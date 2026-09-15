using NotificationBarrage.Domain;
using NotificationBarrage.Services;

var clock = new ManualClock();
var filter = new MessageFilter();
var settings = new AppSettings();
var checks = 0;
void Check(bool ok, string name) { if (!ok) throw new Exception(name); checks++; Console.WriteLine($"PASS {name}"); }
IncomingNotification Incoming(string appId, string body, Guid? id = null, string? name = null) => new(id ?? Guid.NewGuid(), appId, name ?? appId, "好友", body, clock.GetUtcNow());
settings = settings.MergeDiscoveredSources([new("wechat.app", "微信"), new("qq.app", "QQ")]).WithEnabledSourceIds(["wechat.app", "qq.app"]);
BarrageMessage Message(string body, string appId = "qq.app") { filter.TryCreate(Incoming(appId, body), settings, out var m); return m!; }
Check(filter.TryCreate(Incoming("wechat.app", "  你好\n世界 ", name: "微信"), settings, out var first) && first!.Body == "你好 世界", "精确来源与空白规范化");
Check(!filter.TryCreate(Incoming("wechat.preview", "消息", name: "微信"), settings, out _), "同名不同 ID 不被放行");
Check(!filter.TryCreate(Incoming("qq.app", "消息"), settings.WithEnabledSourceIds(["wechat.app"]), out _), "关闭来源后过滤");
filter.TryCreate(Incoming("qq.app", "排队消息"), settings, out var queuedBeforeDisable);
Check(!BarrageDisplayGate.ShouldDisplay(queuedBeforeDisable!, settings.WithEnabledSourceIds(["wechat.app"]))
    && BarrageDisplayGate.ShouldDisplay(queuedBeforeDisable! with { IsTest = true }, settings.WithEnabledSourceIds([])), "展示前过滤禁用积压且测试弹幕独立");
var empty = Incoming("qq.app", " ") with { Title = " " };
Check(!filter.TryCreate(empty, settings, out _), "空通知不显示");
Check(filter.TryCreate(empty with { Title = "标题" }, settings, out var titleOnly) && titleOnly!.Title == "标题", "仅标题通知可显示");
filter.TryCreate(Incoming("qq.app", string.Concat(Enumerable.Repeat("😀", 30))), settings with { MaxBodyLength = 20 }, out var emoji);
Check(emoji!.Body.EndsWith('…') && !emoji.Body.Contains('\uFFFD'), "Unicode 截断完整");
var discovered = new AppSettings().MergeDiscoveredSources([new("telegram.app", "Telegram")]);
Check(!discovered.IsSourceEnabled("telegram.app"), "新发现来源默认关闭");
var draft = new SourceSelectionDraft([new("telegram.app", "Telegram")], ["telegram.app"]);
draft.SetEnabled("telegram.app", false);
draft.MergeSources([new("telegram.app", "Telegram Desktop"), new("signal.app", "Signal")], ["telegram.app", "signal.app"]);
Check(!draft.IsEnabled("telegram.app") && draft.IsEnabled("signal.app"), "刷新保留未保存选择");
var queue = new BarrageQueue(clock);
var duplicate = Message("同一内容");
Check(queue.Enqueue(duplicate) && !queue.Enqueue(duplicate), "通知 ID 去重");
Check(!queue.Enqueue(Message("同一内容")), "不同 ID 相同正文短时去重");
Check(queue.Enqueue(Message("同一内容", "wechat.app")), "不同 AppId 相同正文不去重");
clock.Advance(TimeSpan.FromSeconds(1));
Check(queue.Enqueue(Message("二")) && queue.Enqueue(Message("三")) && queue.Enqueue(Message("四")) && !queue.Enqueue(Message("五")), "每秒最多接收三条，重复不占限流名额");
clock.Advance(TimeSpan.FromSeconds(1));
Check(queue.Enqueue(Message("六")), "滑动限流恢复");
Check(queue.TryDequeue(out var next) && next!.Body == "同一内容", "先进先出");
queue.MarkActiveCompleted();
queue = new BarrageQueue(clock);
for (var i = 0; i < 6; i++) { clock.Advance(TimeSpan.FromSeconds(1)); queue.Enqueue(Message($"并发{i}")); }
for (var i = 0; i < 5; i++) Check(queue.TryDequeue(out _), $"分配第{i + 1}条活动消息");
Check(!queue.TryDequeue(out _) && queue.ActiveCount == 5, "最多同时显示五条");
queue.MarkActiveCompleted();
Check(queue.TryDequeue(out _), "完成后释放展示名额");
queue = new BarrageQueue(clock);
queue.SetPaused(true);
for (var i = 0; i < 20; i++) { clock.Advance(TimeSpan.FromSeconds(1)); queue.Enqueue(Message($"暂停{i}")); }
Check(queue.Count == 10 && !queue.TryDequeue(out _), "暂停只保留最近十条");
queue.SetPaused(false);
Check(queue.TryDequeue(out var resumed) && resumed!.Body == "暂停10", "恢复从最近十条开始");
queue = new BarrageQueue(clock);
for (var i = 0; i < 120; i++) { clock.Advance(TimeSpan.FromSeconds(1)); queue.Enqueue(Message($"积压{i}")); }
Check(queue.Count == 100, "待显示队列有界");
var processor = new NotificationSnapshotProcessor();
var oldNotification = NotificationMapper.Map(1, "qq.app", "QQ", ["旧联系人", "旧内容"], clock.GetUtcNow());
var emptyNotification = NotificationMapper.Map(3, "telegram.app", "Telegram", [], clock.GetUtcNow());
var newNotification = NotificationMapper.Map(2, "qq.app", "QQ", ["联系人", "新内容"], clock.GetUtcNow().AddSeconds(1));
Check(processor.Update([oldNotification, emptyNotification]).Count == 0 && processor.Sources.Any(s => s.AppId == "telegram.app"), "启动发现空通知但不回放");
Check(processor.Update([oldNotification, emptyNotification, newNotification]).Single() == newNotification, "快照只输出新通知");
Check(processor.Update([oldNotification, emptyNotification, newNotification]).Count == 0, "事件和轮询不重复输出");
processor.Reset();
Check(processor.Update([oldNotification, emptyNotification, newNotification]).Count == 0, "权限恢复后不重放历史通知");
Check(NotificationMapper.Map(1, "微信", "微信", ["标题"], clock.GetUtcNow()).Id != oldNotification.Id, "不同来源的相同通知 ID 不冲突");
foreach (var position in Enum.GetValues<BarragePosition>())
{
    var layout = OverlayLayout.Calculate(720, settings with { Position = position, FontSize = 72, VerticalOffset = 500 });
    Check(layout.BandTop >= 0 && layout.BandTop + layout.LaneHeight * layout.LaneCount <= 720, $"{position} 大字号不越出屏幕");
    var viewport = OverlayViewport.Calculate(1280, 720, layout);
    Check(viewport.Height < 720 && viewport.Top >= 0 && viewport.Top + viewport.Height <= 720, $"{position} 覆盖区域限制在弹幕带");
}
var temp = Path.Combine(Path.GetTempPath(), "BarrageChecks-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new SettingsStore(temp);
    Check(store.Load().Position == BarragePosition.Bottom && store.Load().EnabledSourceIds.Length == 0, "新安装默认来源全关");
    store.Save(settings with { Opacity = double.NaN, FontSize = 999 });
    Check(store.Load().Opacity == 0.92 && store.Load().FontSize == 72, "配置持久化及边界校验");
    File.WriteAllText(Path.Combine(temp, "settings.json"), "{\"EnableQQ\":true,\"EnableWeChat\":false}");
    var legacy = store.Load().MergeDiscoveredSources([new("Tencent.QQ_abc!App", "QQ"), new("Tencent.WeChat_xyz!App", "微信")]);
    Check(legacy.IsSourceEnabled("Tencent.QQ_abc!App") && !legacy.IsSourceEnabled("Tencent.WeChat_xyz!App"), "旧 QQ 微信选择迁移为精确 ID");
    File.WriteAllText(Path.Combine(temp, "settings.json"), "{bad");
    Check(store.Load().EnabledSourceIds.Length == 0 && Directory.GetFiles(temp, "*.broken-*").Length == 1, "损坏配置备份并恢复");
    var logger = new AppLogger(Path.Combine(temp, "logs"));
    logger.Error("test", new Exception("不得保存的聊天内容"));
    logger.Info("oversize", new { Details = new string('x', 3 * 1024 * 1024) });
    var log = Directory.GetFiles(Path.Combine(temp, "logs"));
    Check(log.All(p => new FileInfo(p).Length <= 2 * 1024 * 1024), "日志大小限制");
    Check(log.All(p => !File.ReadAllText(p).Contains("不得保存")), "异常消息不落盘");
}
finally { if (Directory.Exists(temp)) Directory.Delete(temp, true); }
Console.WriteLine($"{checks} checks passed.");

sealed class ManualClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan amount) => _now += amount;
}

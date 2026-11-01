# BaseUnity selective modules — v0.26

Các module cài riêng: **Core, Save, Audio, Pooling, MobileInput, GameFlow, UI, Wallet/Rewards, Shop, Daily Reward, Quests, Achievements**.
Mỗi chức năng kinh tế có UI module riêng; prefab mẫu chỉ sinh khi bấm tạo mẫu. Source chỉ được đóng gói theo catalog.
Localization, GameFlow Economy, Ads/Consent/Firebase facades, simulation và SDK adapter cũng có package riêng. Các package SDK không kèm SDK vendor. Save/config legacy không tự migrate.

## Cài vào project mới

1. Tạo project Unity 6 sạch.
2. Đặt Git submodule ở **ngoài Assets và Packages**, ví dụ:

   ```text
   YourGame/
     Assets/
     Packages/
     ProjectSettings/
     Tools/BaseUnity/       <- Git submodule
   ```

   ```sh
   git submodule add https://github.com/user12061999/BaseUnity.git Tools/BaseUnity
   git submodule update --init --recursive
   ```

3. Copy **thư mục `Tools/BaseUnity/Editor/Modularity`** vào `Assets/Editor/BaseUnityModules`.
   Thư mục này chỉ chứa Module Manager/Store và asmdef Editor, không tham chiếu BaseUnity.Runtime.
   Không copy Runtime/Editor/Tests của toàn bộ repo vào Assets. Không cài root package `com.baseunity.foundation`.
4. Mở **Tools > BaseUnity > Module Manager**, chọn repository `Tools/BaseUnity`, bấm Load.
5. Chọn module, Preview changes, rồi Apply. Đợi Unity resolve package và compile.
6. Bấm **Create bootstrap with installed modules**. Lưu scene rồi thêm gameplay của game.

Không cần thêm root repository vào manifest UPM. Module Manager tạo các **embedded packages**
ở `Packages/com.baseunity.*`; Unity tự nhận package. File `Packages/manifest.json` của game không bị viết lại.
Commit các embedded package đã chọn và Module Manager cùng game để build có thể chạy mà không cần bước generate lại.
Source submodule vẫn ở Tools, giúp theo dõi phiên bản và cập nhật.

## Dependency

| Chọn | Package thực sự cài | Assembly |
|---|---|---|
| Core | core | BaseUnity.Core |
| Save | core + save | BaseUnity.Save |
| Audio | core + save + audio | BaseUnity.Audio |
| Pooling | core + pooling | BaseUnity.Pooling |
| MobileInput | core + mobile-input + Unity UI | BaseUnity.MobileInput |
| GameFlow | core + gameflow | BaseUnity.Flow |
| UI | core + ui + Unity UI + built-in Audio | BaseUnity.UI |
| GameFlow UI bridge | core + gameflow + ui + gameflow-ui | BaseUnity.FlowUI |
| Wallet/Rewards | core + save + wallet-rewards | BaseUnity.Wallet |
| Shop | wallet-rewards + shop (và dependency của Wallet) | BaseUnity.Shop |
| Daily Reward | wallet-rewards + daily-reward | BaseUnity.Daily |
| Quests | wallet-rewards + objectives + quests | BaseUnity.Quests |
| Achievements | wallet-rewards + objectives + achievements | BaseUnity.Achievements |
| Wallet UI | wallet-rewards + ui + wallet-ui | BaseUnity.WalletUI |
| Shop UI | shop + wallet-ui + shop-ui | BaseUnity.ShopUI |
| Daily Reward UI | daily-reward + wallet-ui + daily-reward-ui | BaseUnity.DailyUI |
| Quests UI | quests + wallet-ui + quests-ui | BaseUnity.QuestUI |
| Achievements UI | achievements + wallet-ui + achievements-ui | BaseUnity.AchievementUI |

Core có EventBus, ServiceRegistry, StateMachine, TimerService, BuildEnvironment và ModuleBootstrap.
Không có Save/Audio/Input/FeatureConfig hoặc SDK trong Core.
Audio phụ thuộc Save vì AudioService hiện lưu volume qua ISaveStore. Save dùng built-in JSONSerialize;
Audio dùng built-in Audio; MobileInput yêu cầu Unity UI 2.0.0. Pooling không kéo UniTask.
Không module nào trong đợt này kéo Ads, Firebase, consent, IAP, shop hay prefab Layer Lab.

`ModuleBootstrap` là bootstrap tối thiểu mới. `GameBootstrap` cũ không nằm trong Core package.
ModuleInstaller trên cùng root được cài theo Priority; SaveModule chạy trước AudioModule.
Chỉ attach installer cho dịch vụ game cần. Pooling là API, không có installer bắt buộc.

```csharp
using BaseUnity.Core;

public sealed class MyGame : ModuleBootstrap
{
    protected override void OnReady()
    {
        var events = Services.Get<EventBus>();
    }
}
```

Khi cài Save và attach SaveModule, có thể lấy `Services.Get<BaseUnity.Save.ISaveStore>()` trong OnReady.

Game có asmdef riêng cần thêm reference tới các assembly thực sự sử dụng.
Nếu kế thừa MyGame, thay component ModuleBootstrap mặc định bằng MyGame; giữ các installer trên root.

## Mobile input độc lập

Các component `BaseUnity.MobileInput.TouchInputContext`, `TouchJoystick`, `TouchButton`, `SwipeArea`
không phụ thuộc GameFlow, FeatureServices hay hệ thống profile cũ.

- Attach MobileInputModule cùng ModuleBootstrap để đăng ký ManualInputSource/InputRouter.
- Tạo TouchInputContext trên Canvas/UI root và gán context cho control.
- Joystick cần RectTransform area, handle; TouchButton có action mặc định Fire.
- Canvas cần GraphicRaycaster; cảnh cần EventSystem/input UI module như các UI Unity thông thường.
- Game đọc `Services.Get<InputRouter>()` và đặt Enabled=false khi pause/ending; nguồn trả về trung tính khi bị khóa.
- Có thể `context.Bind(manual, router)` nếu dùng input source riêng, không cần ModuleBootstrap.
- Điều chỉnh size/position trong RectTransform, deadZone/sensitivity trên joystick. Lưu preference là trách nhiệm game
  hoặc module Save được chọn riêng; phần này không tự kéo hệ thống profile/preferences của legacy.

Các prefab MobileJoystick cũ ở GameFlow vẫn dùng integration cũ. Không kéo chúng vào project Core-only.

## Cập nhật, gỡ và khôi phục

- Pull submodule, Load/Preview rồi Apply để dựng lại package theo source mới.
- Module bắt buộc được tự thêm; muốn gỡ Save phải bỏ Audio trước.
- Bỏ chọn module và Apply để gỡ. Select none + Preview/Apply gỡ toàn bộ các package do manager quản lý.
- Các thư mục package/repository do công cụ quản lý phải là path thật, không phải junction/symlink.
- File đã chỉnh trực tiếp trong package, file thêm thủ công hoặc package trùng tên không có receipt sẽ chặn thao tác.
  Hãy đưa sửa đổi về source/fork hoặc tự giải quyết khác biệt; manager không ghi đè chúng.
- Dependency khai báo từ package khác, explicit manifest entry hoặc asmdef (tên/GUID) trong Assets sẽ chặn gỡ module đang dùng.
  Game code trong Assembly-CSharp/serialized scene references không thể được suy luận đầy đủ: gỡ component và
  sửa code game trước khi gỡ module liên quan. Manager không tự chỉnh scene/prefab của game.
- Mỗi lần Apply di chuyển package cũ vào `ProjectSettings/BaseUnityModuleBackups/<timestamp-id>`.
  **Restore last module backup** khôi phục snapshot trước đó, cũng tạo backup của trạng thái hiện tại.
- Nếu thay file trực tiếp khiến receipt không còn sạch, restore cũng bị chặn để giữ chỉnh sửa đó.
- Tạo package ở staging trước khi đổi package đang cài, kiểm tra lại ownership/hash ngay trước khi di chuyển.
  Lỗi file-system trong quá trình chuyển có rollback; lỗi compile/UPM sau import cần tự sửa hoặc Restore backup.
- Export only tạo package trong thư mục trống để kiểm tra/chia sẻ mà không cài vào project đang mở.

## Project BaseUnity hiện tại và migration

Project demo hiện có giữ nguyên bundle v0.22 để không làm mất script references/save/UI đã tùy biến.
Module Manager **từ chối cài bộ package mới chồng lên BaseUnity.Runtime** vì trùng class/GUID.
Dùng project sạch để bắt đầu selective modules; có thể mở Export only trong demo cũ.
Không di chuyển hoặc xóa junction/runtime/SDK của project demo bằng tay trong lúc Editor đang mở.
Module mới không phải toàn bộ demo cũ. Prefab Layer Lab, Store, Quest và config legacy cần migration riêng.

## Kiểm chứng trong project sạch

Đã chạy Unity 6000.3.26f1 batchmode với manifest ban đầu không có BaseUnity:
Core-only tạo đúng một assembly BaseUnity.Core; Core+MobileInput tạo đúng hai assembly;
chọn cả năm tạo đúng năm assembly. Cả ba không có BaseUnity.Features.FeatureServices.
Module Manager cũng biên dịch trong project sạch mà không tham chiếu bundle cũ.
Đợt v0.24: 15 EditMode + 2 PlayMode đạt (logic, package lifecycle, view và luồng scene thật).
GameFlow-only chỉ có Core/Flow, UI-only chỉ có Core/UI; kết hợp bridge có đúng 4 assembly.
Bảy prefab không thiếu script; GameRoot giữ các persistent button target sau khi lưu.
Đây là kiểm thử tập trung cho phần modular mới; không phải chạy lại toàn bộ suite legacy.

## GameFlow độc lập (v0.24)

Attach `BaseUnity.Flow.GameFlowModule` cùng root ModuleBootstrap. Auto-start Home sau khi bootstrap sẵn sàng.
Bỏ trống homeScene/ingameScene cho game một scene; nếu dùng nhiều scene, gán đường dẫn scene đã thêm vào build settings.
Lấy `Services.Get<BaseUnity.Flow.GameFlowService>()`, theo dõi Changed, Phase, Level, CanPlay, CanNext.

- HomeAsync → PlayAsync → Win/Lose → RetryAsync/NextAsync/HomeAsync.
- Pause/Resume chỉ chạy khi đúng trạng thái; mất focus tự Pause, người chơi Resume chủ động.
- Level từ 1 đến levelCount; Next chỉ sau Win và không vượt level cuối; Retry giữ level.
- Loading chặn lệnh trùng; tải scene thất bại trả lỗi và khôi phục phase/level trước đó.
- Component cung cấp entry point UnityEvent Home/Play/Pause/Resume/Win/Lose/Retry/Next và ghi lỗi Console.
- Game kiểm tra **CanPlay** để khóa gameplay/input/timer. Module không thay đổi Time.timeScale.
- Với single-scene, game tự reset vật thể khi vào Playing qua Changed. Với nhiều scene, thêm gameplay vào Ingame.
- Tiến trình theo phiên chạy: không tự lưu level, cấp reward hay chọn level prefab. Tích hợp Save riêng khi cần.

## UI và bộ mẫu tùy chọn (v0.24)

UI cần Core, Unity UI và built-in Audio; không cần GameFlow, AudioService hoặc Save.
UIModule đăng ký UIService: ShowScreen/PushPopup/Back/ClosePopups. UIPanel cung cấp Show/Hide override.
UITheme/Binding quản lý background, primary, text, font/size. Sau khi đổi theme runtime, gọi Apply trên binding hoặc bật lại UI.
UIButtonFeedback hỗ trợ press scale/reducedMotion và clicked (cả submit bàn phím).
UIAudioFeedback là âm thanh cục bộ tùy chọn: gán clip, nối Play vào clicked. Clip mặc định trống;
volume/mute độc lập AudioService. Có thể nối clicked sang AudioService của game thay thế.

Chọn **Game Flow UI bridge** để UI tự đổi theo flow. GameFlowView đặt trên root luôn active,
gán panel/levelLabel/nextButton; chỉ dùng một view điều khiển cùng UIService.

Sau khi compile, bấm **Create optional portrait GameFlow/UI sample** trong Module Manager:

- Chọn thư mục mới trong Assets; không ghi đè nội dung sẵn có.
- Sinh scene Bootstrap/Home/Ingame, Theme.asset và **7 prefab: GameRoot, Home, Ingame, Loading, Pause, Win, Lose**.
- Panel là nested prefab. Sửa prefab hoặc tạo variant để đổi theme/layout; GameRoot giữ wiring.
- Canvas 720×1280 màn dọc; GameRoot gồm bootstrap/UI/EventSystem, tồn tại qua scene load.
- Công cụ bổ sung 3 scene vào build settings và giữ entries cũ. **Mở Bootstrap để Play**;
  nếu build, đặt Bootstrap mẫu đầu danh sách scene của game.
- Home có Play; Ingame có Pause và Demo Win/Demo Lose; Pause có Resume/Retry/Home;
  Win có Next/Retry/Home; Lose có Retry/Home. Next khóa ở level cuối.
- Thêm game vào Ingame, gọi Win/Lose từ gameplay thực tế và bỏ hai nút Demo khi hoàn thiện.
- Không copy Layer Lab/clip âm thanh/Shop/Quest/SDK. Không tự khóa hướng màn hình trong Player Settings.
- Sample tạo trong Assets thuộc game: Module Manager không tự cập nhật hay xóa. Trước khi gỡ module,
  gỡ sample/component và sửa code game đang tham chiếu module đó.

## Wallet / Rewards, Shop, Daily Reward, Quest / Achievement (v0.25)

Các API ở namespace `BaseUnity.EconomyModules`. Cài module nào thì attach installer của module đó trên root ModuleBootstrap.
Module Manager có thể tạo root này; gán catalog/schedule trước khi Play. Thứ tự: Save (-100), Wallet (-80), các service chức năng (-60).
Không cần GameFlow hoặc UI nếu chỉ dùng logic. Quests và Achievements dùng chung **objectives** engine nhưng không phụ thuộc nhau.

### Wallet và giao dịch

`EconomyStore` hỗ trợ nhiều currency/item theo ID. `Balance`, `Owned` trả số dư/số lượng; `Grant(receipt, rewards)` cấp thưởng;
`Spend(currency, amount, receipt)` trừ tiền; `Transact` là API giao dịch tùy chỉnh.

```csharp
using BaseUnity.Core;
using BaseUnity.EconomyModules;

var wallet = ModuleBootstrap.Instance.Services.Get<EconomyStore>();
var result = wallet.Grant("level.reward:" + matchId,
    new EconomyReward { id = "coin", amount = 100 });
```

- `TransactionResult`: Success, Duplicate, Rejected. Khi retry, dùng **cùng receipt** cho cùng thao tác; không tạo ID mới để retry một reward đã lưu.
- Receipt lưu cùng tiền/vật phẩm/tiến độ/claim trong một slot JSON. Tính toán trên bản sao, lưu thành công rồi mới đổi trạng thái và phát Changed.
- Thiếu tiền, vượt giới hạn hoặc overflow không làm thay đổi dữ liệu. Lỗi IO được trả bằng exception, không giả báo thành công.
- Nếu observer Changed lỗi, giao dịch đã lưu vẫn thành công; lỗi observer được báo qua ObserverError.
- ID currency/item/catalog 1–64 chữ/số/underscore/hyphen; không âm. Amount dùng long; không tự quy đổi coin/gem.
- Mặc định slot `baseunity_modular_economy`; **không dùng chung slot với OfflineGameProfile legacy**.
- Dữ liệu module đã gỡ vẫn giữ trong slot, có thể tiếp tục khi cài lại. Giữ ID định nghĩa ổn định khi cập nhật nội dung.
- Ledger receipts và các kỳ daily quest được giữ để chống nhận trùng; hiện chưa compact/prune. Phát progress theo sự kiện có ý nghĩa, không ghi save mỗi frame.
- Đây là dữ liệu offline, không xác thực giao dịch qua server và không phải hệ thống IAP.

### Shop

Tạo **Create > BaseUnity > Modules > Shop Catalog**, gán ShopModule.catalog.
Offer có ID, title, currency, price, purchaseLimit (0 = không giới hạn) và reward currency/item.
`ShopService.CanBuy`, `PurchaseCount`, `Buy(offerId, transactionId)` kiểm tra và lưu phí + reward + số lần mua cùng giao dịch.
Item sở hữu đọc qua Wallet.Owned; chức năng equip/sử dụng item do game triển khai.
Giá và reward được snapshot khi bootstrap; đổi catalog cần khởi tạo lại service. Shop mẫu chỉ dùng tiền game.

### Daily Reward

Tạo **Daily Reward Schedule**, gán DailyRewardModule.schedule (bắt buộc).
Schedule có ID, danh sách ngày thưởng và cooldownSeconds (mặc định 86400).
`DailyRewardService` cung cấp DayCount, NextDay (0-based), RewardsAt, RemainingSeconds, CanClaim, Claim.
Chu kỳ quay lại ngày đầu sau ngày cuối; bỏ lỡ thời gian không mất chuỗi và không tự nhận bù nhiều ngày.
Đây là **cooldown tính từ lần nhận trước**, không phải reset lúc nửa đêm. Lưu claim count + thời điểm + reward cùng giao dịch.
Chỉnh lùi đồng hồ không mở lượt nhận trước thời điểm đã lưu. Giờ offline vẫn có thể bị chỉnh tiến;
muốn thời gian đáng tin cần clock/server riêng (constructor nhận Func<long> UTC seconds).

### Quest và Achievement

Tạo Quest Catalog / Achievement Catalog. Definition có ID, title, eventId, target, rewards.
Quest có dailyReset tùy chọn theo **ngày UTC**; Achievement luôn tích lũy, không daily reset.
Tiến độ được chặn tại target; claim cần đủ tiến độ và chưa nhận. Claim và reward lưu cùng giao dịch.
Ngày đã được ghi nhận không quay ngược khi người chơi chỉnh lùi giờ. Trạng thái mới qua Statuses; đổi định nghĩa cần khởi tạo lại service.

Game có thể gọi `QuestService.Record(eventId, amount, receipt)` / AchievementService tương tự, hoặc phát chung:

```csharp
var services = ModuleBootstrap.Instance.Services;
services.Get<EventBus>().Publish(new GameplayProgressEvent("win", 1, "match:" + matchId));
```

Một event cập nhật các objective khớp eventId của từng module đã cài. Cùng receipt không cộng tiến độ lại;
Quest và Achievement có receipt namespace riêng. Khi một listener lưu lỗi, EventBus trả AggregateException;
retry cùng event receipt để hoàn tất các module chưa lưu, không cộng lại module đã lưu.
`Claim(objectiveId)` cấp reward; xem Statuses để hiển thị progress/claimed/CanClaim.
Không tự nối vào Win/Lose của GameFlow: game phát event ở thời điểm xác nhận kết quả, dùng matchId ổn định.

### Prefab/UI mẫu tùy chọn

Chọn từng module Wallet UI, Shop UI, Daily Reward UI, Quests UI, Achievements UI theo nhu cầu.
Shop UI không kéo Daily Reward/Quest/Achievement/GameFlow. Các view tự bind ModuleBootstrap và cập nhật sau giao dịch;
button mua/nhận bị khóa theo trạng thái. Lỗi save hiện thông báo retry và giữ dữ liệu chưa commit.

Bấm **Create optional Economy UI sample (installed features only)** trong Module Manager, chọn thư mục mới trong Assets:

- Sinh scene **EconomySample**, Theme.asset, prefab **EconomyRoot** và **WalletBar**.
- Chỉ khi đã cài UI tương ứng mới sinh **ShopPopup**, **DailyRewardPopup**, **QuestPopup**, **AchievementPopup** và catalog cần dùng.
- Shop mẫu: red_skin giá 100 coin (mua 1 lần); gem_pack giá 250 coin đổi 10 gem (không giới hạn).
- Daily mẫu 7 ngày, 50 → 350 coin, cooldown 24 giờ; quest win 3 lần/ngày UTC thưởng 100 coin;
  achievement win 3 lần tổng thưởng 200 coin.
- Home có nút mở từng popup và **Demo starter (once)**: nhận 1000 coin + 10 gem đúng một lần.
  Quest/Achievement popup có Demo win +1 để thử tiến độ; đây là nút test cần bỏ khỏi game phát hành.
- Sample dùng slot riêng **baseunity_modular_economy_demo**, không tự cấp thưởng khi startup.
- Sửa layout trên prefab, giao diện trên Theme.asset, nội dung trong catalog/schedule. Không có ảnh/clip/vendor SDK được tự import.
- Có thể kéo WalletBar/popup vào Canvas game đã có bootstrap + installer. Các nút Close của popup tự đóng panel;
  UIService bỏ qua panel đã đóng khi Back. **EconomyRoot chứa bootstrap/EventSystem**, chỉ dùng một root cho một phiên.
- Scene sample được thêm vào build settings nhưng không thay scene khởi chạy cũ. Mở EconomySample để Play.
- Module Manager không ghi đè/gỡ nội dung Assets đã sinh. Gỡ prefab/component tham chiếu trước khi gỡ module.
- Đây là mẫu mới độc lập; không tự chuyển Store/Quest prefab Layer Lab hoặc save của legacy.

### Kiểm chứng v0.25

32 EditMode + 5 PlayMode đạt trong project kiểm thử modular sạch, gồm các kiểm tra v0.24 được giữ lại.
Đã kiểm tra giao dịch/receipt, lỗi lưu, overflow, catalog, reload save, cooldown, reset ngày UTC, EventBus,
UI button states và scene/prefab mẫu qua mở lại game. Không thiếu script hoặc persistent button target trong EconomyRoot.
Wallet-only, Shop UI-only và Quests-only biên dịch thành công ở các project riêng.
Shop UI-only sinh đúng 3 prefab (WalletBar, ShopPopup, EconomyRoot), không sinh daily/quest/achievement config/UI.
Đây là kiểm thử tập trung cho phân phối modular; chưa chạy lại toàn bộ suite của project legacy hay kiểm thử thiết bị thật.

## Preset và kiểm tra build (v0.26)

Sau khi pull repo, copy lại **toàn bộ Editor/Modularity** sang project game để cập nhật manager/validator cùng phiên bản catalog.
Trong Module Manager chọn preset rồi **Use preset → Preview changes → Apply**:

- Minimal: chỉ Core.
- GameFlow + UI: Core + GameFlow + UI + bridge.
- Mobile Offline: luồng game/UI, input, audio/pooling, economy/UI, localization/UI và GameFlow Economy; không có SDK/ads/firebase.
- SDK simulation: facade + provider mô phỏng Ads/Consent/Firebase; không có SDK vendor và không kéo Wallet/GameFlow.

Use preset **thay thế lựa chọn hiện tại**; Preview hiển thị các package sẽ cài/gỡ. Dòng summary hiển thị dependency và số script,
không tính mã SDK vendor. Bạn vẫn có thể sửa từng checkbox trước Apply. Backup và bảo vệ file đã chỉnh giữ nguyên.

**Tools > BaseUnity > Validate Modular Build** (hoặc nút trong manager) kiểm tra scene enabled trong Build Settings bằng preview scene.
Kiểm tra không khởi tạo SDK và không lưu/thay scene đang mở: missing script/reference, panel/button chưa gán,
bootstrap/installer/dependency, scene GameFlow, catalog/schedule/config bắt buộc, ID trùng, reward/target/cooldown không hợp lệ,
provider/SDK/App ID/app config bị thiếu và mô phỏng trong release.
`IPreprocessBuildWithReport` tự chạy kiểm tra trước build; lỗi chặn build, warning được ghi Console.
Simulation chỉ được phép trong Development Build; build hook lấy cờ từ BuildReport, không chỉ dựa vào checkbox Editor.
Validator kiểm tra wiring/config, không chứng minh SDK có thể lấy quảng cáo, kết nối mạng hay cấu hình account/server đúng.

## Localization độc lập (v0.26)

`localization` cần Core + Save, không cần UI; `localization-ui` thêm Unity UI.
Attach `BaseUnity.Localization.LocalizationModule`, chọn defaultLanguage en/vi; table tùy chọn.
Tạo **BaseUnity > Modules > Localization Table** để thêm key/English/Vietnamese hoặc ghi đè built-in.
API: `SetLanguage`, `Text`, `Format(key, args)`, `Number(value, format)`, `Language`, `Culture`, `Changed`, `LastError`.
Ngôn ngữ lưu ở slot riêng `baseunity_modular_language`; lỗi lưu không đổi ngôn ngữ hiện tại.
Thiếu key trả về source; VI dùng vi-VN, EN dùng en-US khi Format/Number. Built-in phủ các nhãn và template của UI mẫu.

- `LocalizedText` cho Text theo key cố định.
- `LocalizedCanvas` cho mẫu có cả Text động: giữ source English riêng và cập nhật lại sau khi số dư/tiến độ thay đổi.
  Gọi Capture sau khi thêm Text mới runtime. Dùng LocalizedText hoặc LocalizedCanvas cho cùng Text, tránh hai component cùng ghi.
- Template nhận dạng từ source English dùng `{0}`…`{7}`; template VI có thể dùng format số, ví dụ `Xu: {0:N0}`.
- Đây là Unity UI Text; không tự import TMP, font mới hoặc localization package của Unity.
- Khi localization-ui đã cài, các generator GameFlow/Economy tự thêm LocalizationModule và nút EN/VI.

## Cầu nối GameFlow → Rewards / Quest / Achievement (v0.26)

Chọn `gameflow-economy`: cần GameFlow + Wallet/Save + Objective engine; **không tự kéo Quests/Achievements/UI**.
Attach `BaseUnity.FlowEconomy.FlowEconomyModule` cùng bootstrap, gán FlowRewardConfig (Create > BaseUnity > Modules).
Config gồm winRewards (mặc định 100 coin), loseRewards (mặc định rỗng), publishProgress.
Cài/attach QuestModule và AchievementModule riêng nếu game cần chúng.

- Mỗi lần vào Playing thành công có **GameFlowService.RunId** mới. Pause/Resume giữ ID; Retry/Next sinh ID mới.
- Completed phát snapshot GameOutcome(runId, level, won). Lệnh rời result bị khóa trong khi các callback xử lý outcome.
- Bridge lưu reward + metadata + outbox trong **một** giao dịch Wallet, receipt `flow:<runId>:reward`.
- Sau đó phát GameplayProgressEvent `play` và `win`/`lose`, có receipt theo runId. Các module đã cài cập nhật tiến độ và dedup riêng.
- Chỉ xóa trạng thái pending sau khi listener và acknowledgement lưu thành công. Restart sẽ đọc lại outbox để tiếp tục;
  phát lại không cộng reward/progress đã lưu.
- Nếu lưu lỗi: HasPending/LastError cho biết tình trạng; module thử lại mỗi giây hoặc gọi RetryPending.
  Home/Retry/Next từ result bị chặn khi còn pending; GameFlowView có resultStatus tùy chọn để hiện trạng thái đang lưu.
- Outcome chưa kịp lưu lần đầu chỉ giữ trong RAM: nếu tắt app/crash trước lần lưu thành công thì không thể khôi phục kết quả đó.
  Không có bảo đảm exactly-once qua crash trước journal; outbox bảo vệ phần đã commit.
- Không gọi wallet.Grant hoặc phát lại event win thủ công cho cùng kết quả khi đã dùng bridge; tránh tạo receipt khác cho cùng reward.
- Namespace `flow:` trong Wallet do bridge quản lý. Journal/receipt chưa compact; level hiện tại vẫn theo phiên chạy của GameFlow.

Khi bridge đã cài, **Create optional portrait GameFlow/UI sample** tự thêm Save/Wallet/FlowRewardConfig và bridge;
nếu đã cài Quests/Achievements thì thêm catalog win_3 và installer. Nếu Wallet UI đã cài, Home hiện số dư.
Mẫu dùng slot `baseunity_modular_game_demo`, khác slot Economy sample. Có thể kéo QuestPopup/AchievementPopup vào Canvas
đã có các service tương ứng; dùng cùng catalog ID để hiện tiến độ thật.

## Ads / Consent / Firebase theo package (v0.26)

| Chọn | Phần được import | SDK cần sẵn |
|---|---|---|
| integrations-core | status, async cancellation | Không |
| consent | ConsentService + ConsentModule/provider API | Không |
| ads | AdsService + AdsModule, phụ thuộc consent | Không |
| firebase | FirebaseService + FirebaseModule, local defaults | Không |
| consent-simulation / ads-simulation / firebase-simulation | provider mô phỏng, chọn riêng từng loại | Không |
| consent-google-ump | GoogleUmpProvider | Google Mobile Ads UMP |
| ads-admob | AdMobProvider | Google Mobile Ads |
| ads-max | AppLovinMaxProvider | AppLovin MAX (MaxSdk.Scripts); Unity Web Request dependency được thêm |
| firebase-sdk | FirebaseSdkProvider + Android library/iOS collection build hook | Firebase App + Analytics + Crashlytics + Remote Config |

Facade giữ API ở namespace **BaseUnity.Features**; installer ở **BaseUnity.IntegrationModules**.
Các contract được tách ra khỏi FeatureConfig/FeatureProviders nhưng giữ tên type để bản legacy tiếp tục biên dịch.
Không chọn adapter thì không có mã gọi SDK trong package facade; không cài SDK qua Module Manager.
Apply trong project hiện tại chặn adapter có sdkTypes chưa được load. Export chỉ đóng gói adapter để chuyển sang project có SDK;
không cam kết export có thể biên dịch khi project nhận chưa cài SDK.

### Thiết lập

1. Chọn facade/provider adapter cần dùng, cài official SDK trước khi Apply adapter thật.
2. Tạo provider asset từ **Create > BaseUnity > Providers** hoặc **BaseUnity > Simulation**.
3. Create bootstrap trong Module Manager; gán provider/config cho các installer.
4. Mở **Tools > BaseUnity > Modular Integration Setup**, chọn bootstrap. Tắt chức năng thì ẩn phần thông tin bên dưới;
   Ads chỉ hiện ID theo format/network đã bật, Firebase chỉ hiện defaults/fetch settings khi bật Remote Config.
5. Ads cần ConsentModule cùng root. Consent mặc định bật và thiếu provider sẽ chặn ads; muốn không dùng consent gate phải tắt rõ settings.enabled.
6. Chọn đúng AdsSettings.network theo provider. Điền App IDs/unit IDs hoặc MAX key và cấu hình **official SDK settings** tương ứng;
   các trường trên module không tự sửa GoogleMobileAdsSettings/AppLovinSettings/manifest của vendor.
7. Firebase cần file chính thức google-services.json hoặc GoogleService-Info.plist đúng app. Adapter package cung cấp tắt auto-collection
   trên Android/iOS; collection bật theo flags khi FirebaseService khởi tạo. UMP gate dùng cho Ads, không tự điều khiển flags Firebase.
8. Validate Modular Build trước khi build thiết bị. Dừng Play mode để chỉnh startup settings trong cửa sổ setup.

Dùng service qua `ModuleBootstrap.Instance.Services.Get<AdsService>()` / ConsentService / FirebaseService.
Ads hỗ trợ LoadAsync/ShowAsync/IsReady/SetEnabled/SetNoAds; reward chỉ khi AdResult.Rewarded, không cấp khi Closed/Unavailable.
Consent cung cấp InitializeAsync/RetryAsync/ShowPrivacyOptionsAsync và CanRequestAds, không coi permission này là flag personalized ads.
Firebase có LogEvent/RecordException/FetchRemoteConfigAsync/GetString/GetBool/GetLong; defaults dùng được offline hoặc khi disabled.
SDK native error trả qua Status/LastError; không tự chuyển sang backend mô phỏng khi SDK lỗi.

### Mô phỏng

Provider mô phỏng chỉ Create được trong Editor/Development player; validator chặn khi build release.
SimulatedAdsProvider mặc định trả **Closed** cho rewarded. Chỉ khi bạn đặt rewardedResult=Rewarded mới thử nhánh cấp thưởng.
SimulatedConsentProvider.allowRequests cho phép thử nhánh chặn ads. SimulatedFirebaseProvider.remoteValues mô phỏng fetch,
events ghi log trong RAM, không gửi về Firebase. Chúng là công cụ test luồng, không phải dữ liệu quảng cáo thật.

### Kiểm chứng v0.26

**50 EditMode + 7 PlayMode đạt** trong project modular: ngôn ngữ/số, preset, validation, session ID, reward/outbox/retry, mô phỏng,
và luồng scene thật Win → coin → Quest/Achievement → mở lại game. Các sample GameFlow/Economy đã qua validator.
Localization-only, GameFlow Economy-only và Ads simulation-only biên dịch trong các project độc lập không có SDK vendor.
Bốn adapter thật biên dịch với SDK hiện có trên máy (Google Mobile Ads, MAX, Firebase); còn cảnh báo API cũ của Google SDK.
Chưa gọi ads thật/Firebase tài khoản, chưa build APK/iOS hoặc xác minh mediation/native consent trên thiết bị.

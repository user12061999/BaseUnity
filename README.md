# BaseUnity Foundation

Base game Unity tái sử dụng qua **Git submodule trong `Assets/BaseUnity`**.
Repository chứa thư viện, không chứa nguyên dự án Unity. Namespace: `BaseUnity`.
Phiên bản hiện tại: **0.8.0**, dành cho Unity **6.0 trở lên**; môi trường kiểm thử: Unity **6000.3.26f1**, Windows.

## Luồng game có sẵn

Chọn **Tools > BaseUnity > Install Game Flow Sample**, chờ compile và mở
`Assets/BaseUnityGameFlow/Scenes/Bootstrap.unity`, sau đó nhấn Play.
Yêu cầu Unity UI (`com.unity.ugui`); phần core không yêu cầu package này.

Mẫu cung cấp **Bootstrap → Loading → Home → InGame → Result**, có Settings,
Pause/Resume, Retry, trở về Home và lưu điểm cao nhất/số phiên chơi.
Menu tạo ba scene + `GameFlowConfig.asset`, đăng ký scene vào Build Settings và giữ nguyên scene khác.
Nếu Build Profile đang ghi đè danh sách scene, thêm ba scene vào profile đó với Bootstrap đầu tiên.
UI được dựng lúc chạy, không cần import TMP Essentials.

Code mẫu nằm trong `Samples~/GameFlow`. Installer copy vào Assets để bạn sửa cho từng game,
không sửa code bên trong submodule. Khi đã cài, chạy **Tools > BaseUnity > Game Flow > Generate Missing Scenes**
để tạo scene còn thiếu; các scene/config có sẵn không bị ghi đè.
UPM cũng hỗ trợ import sample **Game Flow**, sau đó chạy menu tạo scene.
Xem thêm [hướng dẫn mẫu](Samples~/GameFlow/README.md).

### UI Layer Lab tùy chọn

Nếu dự án có **Layer Lab GUI Pro-CasualGame**, chọn **Tools > BaseUnity > Game Flow > Apply Layer Lab UI**.
Menu tạo prefab UI có thể chỉnh sửa và gắn vào `GameFlowConfig.uiPrefab`:
Home, HUD, Settings (Master/Music/Effects, mute), Pause, Result và Loading.
Prefab dùng sprite/font đang có trong dự án; không đưa asset Layer Lab vào submodule.
Chạy lại menu sẽ tạo bản prefab mới để giữ nguyên các chỉnh sửa trước đó.
Bố cục dọc **900×1600**, tự fit trong safe area. Khi cài vào game mobile khác, đặt
Player Settings > Resolution and Presentation > Default Orientation thành **Portrait**.

### Win / Lose / Next Level

`GameFlowConfig.levels` có sẵn 3 màn, mục tiêu 30/50/80 điểm và thời gian 60 giây.
Mỗi màn có thể dùng chung InGame scene hoặc đặt `sceneOverride` riêng.
Đạt mục tiêu sẽ Win khi `autoWinAtTarget` bật; hết thời gian sẽ Lose. Đặt thời gian 0 để bỏ giới hạn.

- `WinLevel()`: kết thúc thắng, lưu và mở khóa màn kế tiếp.
- `LoseLevel()` hoặc `LoseLevel(reason)`: kết thúc thua, không mở khóa màn.
- `RetryLevel()`: chơi lại màn vừa thắng/thua và reset điểm/timer.
- `NextLevel()`: chỉ có hiệu lực sau Win và còn màn kế tiếp.
- `StartLevel(number)`: chọn màn đã mở khóa, đánh số từ 1.

Play ở Home mở màn cao nhất đã mở khóa. Màn cuối hiện **All levels complete**, không có Next Level.
Tiến trình được lưu qua lần khởi động lại; save cũ giữ điểm/số phiên và bắt đầu từ level 1.
`CompleteSession()` cũ vẫn dùng được, tương đương `WinLevel()`.
Level có prefab sẽ tự ẩn các nút thử Collect/Win/Lose và dùng gameplay riêng.

### LevelData, prefab gameplay và chọn màn

- Mỗi entry trong `GameFlowConfig.levels` tham chiếu một `LevelData`: tên, mục tiêu, thời gian, scene và prefab gameplay.
- `flow.Levels` là `LevelManager`, tạo prefab trong scene InGame, dọn prefab cũ khi Retry/Next/Home.
- Prefab triển khai `ILevelGameplay.Initialize(LevelContext)` để nhận `AddScore`, `Win`, `Lose`; callback cũ hoặc lúc Pause bị bỏ qua.
- Home có **Choose Level**: hiển thị màn khóa/đã hoàn thành, phân trang 6 màn, cho chơi lại màn đã mở.
- Gameplay mẫu: chạm mục tiêu vàng để lấy điểm, tránh bẫy đỏ. Có 3 LevelData mẫu trong thư mục `Levels`.

Với bản cài cũ, chạy **Tools > BaseUnity > Game Flow > Generate Level Assets** để tạo dữ liệu từ cấu hình
inline hiện có. Asset/prefab đã có không bị ghi đè. Game dùng scene riêng có thể tạo LevelData thủ công
qua **Assets > Create > BaseUnity > Level Data** và để trống gameplayPrefab.

## Base mobile offline (0.7)

Mở **Tools > BaseUnity > Feature Manager**, chọn `FeatureConfig` gắn vào `GameFlowConfig`:

| Tab | Cấu hình |
| --- | --- |
| Offline | Bật Wallet; tiền ban đầu; thưởng thắng/lần đầu; Shop; chu kỳ Daily Reward; nhiệm vụ |
| Language | Bật Việt/Anh, ngôn ngữ mặc định `en`/`vi`, thêm bản dịch tùy chỉnh |
| Ads / Firebase | Provider, ID, SDK key và file cấu hình của từng dịch vụ |
| Game | Analytics của luồng chơi, fetch Remote Config ở Home, banner Home, interstitial mỗi N lần thắng |
| Setup | Kiểm tra cấu hình, scene, SDK và thông tin ứng dụng |

Tắt module sẽ ẩn các trường con trong cửa sổ và phần UI liên quan; giá trị cũ vẫn được giữ.
Core mặc định tắt các module tùy chọn. Sửa config khi không Play; khởi động lại Play để áp dụng.

Ở **Home > MENU** có Shop, Daily Reward, Missions, Inventory và nút đổi ngôn ngữ.
UI dọc tự fit safe area, dùng sprite nút của prefab Layer Lab nếu có; không cần Layer Lab để chạy.
Shop mua bằng Coin/Gem, thêm vật phẩm, có giới hạn tổng số lần mua theo item ID (0 = không giới hạn).
Inventory hiển thị số lượng; hiệu ứng sử dụng vật phẩm thuộc gameplay của game, gọi `ConsumeItem` khi sử dụng.

Thắng màn nhận `winCoins` và `firstWinBonus` nếu lần đầu hoàn thành level đó; thua chỉ cập nhật nhiệm vụ.
**X2 REWARD** chỉ hiện khi reward/Ads/rewarded đều bật, chỉ cộng thưởng sau callback `Rewarded` của SDK.
Chặn chuyển màn khi đang xem quảng cáo. Đóng video hoặc callback cũ không cấp thưởng.
Khi lưu tiến trình/thưởng lỗi, màn kết quả có **RETRY REWARD SAVE**; sửa lỗi lưu rồi thử lại, không cộng thưởng hai lần.

Daily Reward dùng ngày **UTC trên thiết bị**, chu kỳ tiếp tục ở lần nhận kế tiếp; bỏ ngày không mất chu kỳ.
Nhiệm vụ đếm lượt hoàn thành, lượt thắng hoặc tổng điểm, làm mới theo ngày UTC, mỗi nhiệm vụ nhận một lần/ngày.
Lùi đồng hồ không mở lại phần thưởng đã nhận; đây vẫn là hệ thống offline, không chống chỉnh giờ tiến lên/sửa save.

Wallet, inventory, purchase limits, daily claims, mission progress và thưởng level cùng một hồ sơ JSON.
Mỗi giao dịch ghi thành công rồi mới cập nhật bộ nhớ/phát `Changed`; lỗi ghi không trừ tiền hay nhận thưởng một nửa.
Từ v0.8, khi dùng module offline, tiến trình mở màn và economy được ghi trong **cùng một hồ sơ**.
Kết quả màn chưa được đóng sẽ được khôi phục khi khởi động lại. Xem quy tắc migration và thưởng bên dưới.
Không đổi ID item/mission, thứ tự level hoặc save slot sau phát hành nếu chưa có kế hoạch migration.
Tiền ban đầu chỉ áp dụng cho hồ sơ mới; tắt module không xóa save.

```csharp
var economy = Services.Get<BaseUnity.Features.OfflineService>();
economy.Buy("energy");                 // Kiểm tra tiền + trừ + cấp item trong một lần lưu.
economy.ConsumeItem("energy", 1);      // Game quyết định hiệu ứng sau khi trả true.
economy.AddCurrency(BaseUnity.Features.Currency.Coin, 100);
var language = Services.Get<BaseUnity.Features.LocalizationService>();
language.SetLanguage("vi");
string play = language.Text("Play");
```

`GameFlowFeatures` tự nối `level_start`, `level_end`, `shop_purchase`, `daily_reward`, `mission_claim`,
`rewarded_bonus` vào Firebase khi Analytics được bật. Interstitial chỉ hiện nếu đã tải sẵn ở điểm chuyển màn;
không có quảng cáo vẫn chơi được. Remote Config fetch không tự thay đổi công tắc/tỉ lệ thưởng: gameplay đọc
`FirebaseService.GetLong/GetBool/GetString` với giá trị dự phòng. Crashlytics dùng adapter đã cài và `RecordException`.

Localization có catalog mẫu Việt/Anh và fallback về chuỗi gốc. Thêm bản dịch tên màn/vật phẩm/nội dung game
trong `translations`; UI riêng dùng `LocalizationService.Text` và đăng ký `Changed` để cập nhật.
UI mẫu tự cập nhật Text; thông báo kỹ thuật của SDK và nội dung tùy biến chưa dịch giữ nguyên tiếng Anh.

Bản cài sample cũ: sao lưu chỉnh sửa riêng rồi merge/copy các file thay đổi từ `Samples~/GameFlow/Runtime`.
Installer chỉ thêm file thiếu, không ghi đè code mà dự án đã sửa.

## Lưu dữ liệu và khôi phục (0.8)

- Hồ sơ offline dùng `schemaVersion = 2` bên trong payload; envelope `ISaveStore` vẫn ở version 1 để giữ tương thích API.
- Lần đầu chạy Game Flow 0.8, tiến trình v0.7 từ `GameFlowConfig.saveSlot` được nhập vào `offline.saveSlot`,
  cùng với tiền/vật phẩm đã có. Chỉ đánh dấu `progressMigrated` khi ghi thành công; file tiến trình cũ được giữ nguyên.
- Sau migration, Game Flow chỉ ghi tiến trình vào hồ sơ chung. Tắt economy vẫn tiếp tục lưu tiến trình trong hồ sơ đó,
  đồng thời ngừng cộng thưởng/nhiệm vụ và giao dịch. Game chưa từng bật offline tiếp tục dùng slot tiến trình cũ.
- Giao dịch kết thúc màn ghi điểm, mở khóa màn, nhiệm vụ, tiền thưởng và kết quả cùng một lần. Nếu ghi lỗi,
  dữ liệu trong bộ nhớ không được cập nhật; ở màn Result có thể thử lưu lại.
- Nếu app đóng sau khi lưu kết quả nhưng trước khi người chơi chọn Home/Retry/Next, lần mở sau sẽ vào lại Result.
  Khi rời Result, trạng thái đã đóng được lưu trước khi chuyển màn. Save v0.7 không có đầy đủ thông tin kết quả,
  nên migration không tự suy đoán màn kết quả cũ.
- Sau callback rewarded của SDK, lưu xác nhận thưởng trước rồi mới cộng bonus. Nếu app đóng giữa hai bước,
  xác nhận đã lưu sẽ được xử lý lại đúng một lần lúc khôi phục Result. Không tự cấp thưởng chỉ vì quảng cáo đã mở.
  Nếu app bị kết thúc **trước khi callback/xác nhận được ghi thành công**, base offline không thể chứng minh thưởng;
  người chơi có thể thử lại quảng cáo. Không có bảo đảm khôi phục dữ liệu chưa từng ghi xuống đĩa.

`JsonSaveStore.LoadValidated` phân biệt file chưa tồn tại và file đã tồn tại nhưng lỗi. Hồ sơ không đọc được
hoặc dữ liệu không hợp lệ sẽ thử `.bak`. Nếu dùng backup, lần ghi kế tiếp giữ bản backup tốt và lưu file chính
bị lỗi thành `.corrupt-<id>` để kiểm tra. Backup có thể là trạng thái trước giao dịch mới nhất.
Nếu cả hai hỏng, game hiển thị lỗi và chặn vào chơi; không tự reset tiền/tiến trình. Giữ các file, khôi phục
một bản backup hợp lệ rồi khởi động lại game. Schema mới hơn phiên bản code hiện tại cũng bị chặn, tránh downgrade.
API `TryLoad` cũ vẫn giữ hành vi tương thích; dữ liệu quan trọng nên dùng `LoadValidated`.

Các game tự viết flow có thể dùng `OfflineService.InitializeProgress`, `FinishSession`, `PendingResult`,
`RecordRewardedCompletion`, `RecoverPendingBonus`, `AcknowledgeResult`. Callback quảng cáo phải được kiểm chứng
bởi adapter trước khi gọi `RecordRewardedCompletion`; đây không phải hệ thống xác thực server.
Save vẫn là JSON cục bộ, không chống sửa dữ liệu hoặc rollback thủ công; kiểm thử hiện trên Windows/Unity Editor.

## Prefab UI offline có thể chỉnh sửa (0.8)

Chọn `GameFlowConfig`, chạy **Tools > BaseUnity > Game Flow > Generate Missing Offline UI Prefabs**.
Generator tạo thư mục `OfflineUI` cạnh config và gắn `offlineMenuPrefab`:

| Prefab | Chỉnh sửa |
| --- | --- |
| `OfflineMenu.prefab` | Khung popup, wallet, tab, dấu tab đang chọn, ngôn ngữ, Close, thông báo và blocker đang lưu |
| `Shop.prefab` | Bố cục Shop và phân trang |
| `DailyReward.prefab` | Bố cục nhận quà theo ngày |
| `Missions.prefab` | Bố cục tiến độ nhiệm vụ và nút nhận |
| `Inventory.prefab` | Bố cục túi đồ, trạng thái rỗng |
| `ItemRow.prefab` | Tên/số lượng vật phẩm và nút hành động; có thể thay row prefab riêng cho từng panel |

Mỗi panel có `rowPrefab`, `rows`, `pageSize`, nút phân trang và các Text binding. Mặc định 3 dòng/trang;
điều chỉnh chiều cao dòng/panel cùng `pageSize` nếu đổi số dòng. Dữ liệu vẫn lấy từ FeatureConfig.
`OfflineMenuView.animationSeconds` điều khiển fade/scale bằng thời gian không chịu pause; màu thông báo thành công/lỗi
có thể chỉnh trong Inspector. Giao dịch có blocker và khóa thao tác lặp. Popup giữ nguyên instance khi cập nhật dữ liệu.

Generator **không ghi đè prefab có sẵn** và không thay config đã có `offlineMenuPrefab`. Có thể dùng prefab variant
và gắn vào config riêng của từng game. Khi chưa gắn prefab, UI dựng bằng code của v0.7 vẫn hoạt động.
Validate build configuration kiểm tra các binding UI bắt buộc. Sprite Layer Lab được tham chiếu từ dự án,
không kèm asset thương mại trong repository; máy khác chạy generator để tạo prefab tại dự án của mình.

## Cài vào dự án khác

### Ads và Firebase tùy chọn

Mở **Tools > BaseUnity > Feature Manager** để cấu hình bằng popup: bật/tắt chức năng,
chọn AdMob/MAX, nhập App ID/ad-unit ID/SDK key, tạo provider và import file cấu hình Firebase.
Nút **Save** lưu FeatureConfig; nút **Apply app IDs / SDK key** đồng bộ các trường không rỗng
sang SDK đã cài. Tab Firebase hiển thị Project ID/App ID lấy từ file chính thức và kiểm tra
package/bundle ID trước khi import. File cũ được sao lưu khi thay thế.

Tạo **Tools > BaseUnity > Features > Create Feature Config**, gắn vào Bootstrap hoặc
`GameFlowConfig.features`. Bản cài GameFlow mới có sẵn asset này; mặc định cả hai module tắt.

- Ads: chọn **AdMob hoặc AppLovin MAX**, bật/tắt Banner, Interstitial, Rewarded; có test mode, timeout và cooldown.
- Firebase: bật/tắt Analytics, Crashlytics, Remote Config riêng; Remote Config có giá trị mặc định khi offline.
- Core vẫn compile/chạy khi không cài SDK. Menu Install Adapter kiểm tra SDK trước khi copy code vào Assets.
- Config được copy lúc khởi động; thay asset cần khởi động lại Play. Trong runtime dùng `SetEnabled(bool)` trên service.

Xem [hướng dẫn cài SDK, provider và API](Samples~/Integrations/README.md). Adapter source đã có;
SDK bên thứ ba, App ID/ad-unit ID và file cấu hình Firebase thuộc từng dự án game, không nằm trong submodule.

### Git submodule

Chạy ở thư mục gốc của **dự án game đích** đã có Git:

```sh
git submodule add https://github.com/user12061999/BaseUnity.git Assets/BaseUnity
git add .gitmodules Assets/BaseUnity Assets/BaseUnity.meta
git commit -m "Add BaseUnity foundation"
```

Đợi Unity import trước khi add `Assets/BaseUnity.meta`. File này do repository game quản lý;
các file `.meta` bên trong submodule do repository BaseUnity quản lý.

Khi clone dự án game:

```sh
git clone --recurse-submodules <game-repository-url>
# Hoặc nếu đã clone:
git submodule update --init --recursive
```

Cập nhật có chủ đích, sau đó commit con trỏ submodule trong game:

```sh
git -C Assets/BaseUnity fetch origin
git -C Assets/BaseUnity checkout <tested-commit-or-tag>
git add Assets/BaseUnity
git commit -m "Update BaseUnity foundation"
```

Không cài đồng thời bằng submodule và Package Manager vì sẽ trùng assembly/GUID.
Cũng có thể dùng Add package from Git URL với URL repository này.

## Cấu trúc

```text
Runtime/
  Core/          Bootstrap, registry, event bus, state machine
  Time/          Timer theo scaled/unscaled time
  Save/          JSON save slot, version, backup
  Scenes/        Load/unload scene bất đồng bộ
  Audio/         Music, SFX, volume, mute, lưu settings
  UI/            Screen và popup stack
  Input/         Input abstraction, router, touch/manual source
  InputSystem/   Adapter tùy chọn cho Unity Input System
  Pooling/       Prefab pool
  Diagnostics/   Log theo mức và category
Editor/          Menu tạo Bootstrap
Tests/           EditMode và PlayMode tests
Samples~/        Ví dụ, không tự compile khi cài thư viện
package.json
```

`.gitignore` dùng danh sách cho phép ở root. `Assets`, `Packages`, `ProjectSettings`,
`Library`, `Logs`, `Temp`, `UserSettings`, IDE files và công cụ kiểm thử cục bộ không được commit.
Khi thêm thư mục phát hành mới ở root, cập nhật danh sách cho phép này.

## Khởi tạo

Trong scene đầu tiên, dùng **GameObject > BaseUnity > Bootstrap**. Đặt bootstrap trên một
GameObject root riêng. Nó tồn tại qua các scene; bản trùng sẽ bị hủy cùng GameObject của nó.
Không cần prefab, Resources asset hay cấu hình render pipeline đặc biệt.

Game code nên lấy service trong `Start` hoặc `OnReady`, sau khi bootstrap khởi tạo.
Kế thừa `GameBootstrap`, dùng `Configure` để đăng ký service riêng và `OnReady` để vào menu/game:

```csharp
using BaseUnity.Core;
using BaseUnity.Time;

public sealed class MyGameBootstrap : GameBootstrap
{
    protected override void Configure(ServiceRegistry services)
    {
        // services.Register<IMyService>(new MyService());
    }

    protected override void OnReady()
    {
        Services.Get<TimerService>().Schedule(1, () => UnityEngine.Debug.Log("Ready"));
    }
}
```

Chỉ gắn **một** bootstrap, dùng lớp mặc định hoặc lớp kế thừa. Các override vòng đời Unity
phải gọi `base`. Bootstrap đăng ký EventBus, StateMachine, TimerService, ISaveStore,
SceneService, UIService, AudioService, ManualInputSource và InputRouter.
Pool do hệ thống gameplay tạo/giải phóng vì mỗi pool gắn với một prefab và vòng đời riêng.

Registry từ chối đăng ký trùng loại, dispose theo thứ tự ngược, và chỉ dispose một lần nếu
cùng instance đăng ký dưới nhiều interface. Nên truyền service qua constructor vào game logic;
`GameBootstrap.Instance.Services` là điểm truy cập từ MonoBehaviour khi cần.

## Event và state

```csharp
var events = GameBootstrap.Instance.Services.Get<EventBus>();
var subscription = events.Subscribe<int>(coins => UnityEngine.Debug.Log(coins));
events.Publish(100);
subscription.Dispose(); // Khi owner disable/destroy; không bỏ qua bước này.
```

Event dispatch đồng bộ, dùng snapshot: unsubscribe trong callback có hiệu lực từ lần publish kế tiếp.
Mọi listener đều được gọi; lỗi được gom thành `AggregateException` sau dispatch.
Không publish từ background thread. `IGameState` có `Enter`, `Tick`, `Exit`;
`StateMachine.Change(state)` exit state cũ trước khi enter state mới, `Change(null)` dừng state.
Không đổi state bên trong `Enter`/`Exit`; có thể đổi từ `Tick`. StateMachine không tự đổi `timeScale`.

## Save

```csharp
[System.Serializable]
public class PlayerSave { public int level = 1; public int coins; }

// Trong một method:
var saves = GameBootstrap.Instance.Services.Get<BaseUnity.Save.ISaveStore>();
saves.Save("player_1", new PlayerSave { coins = 100 });
if (saves.TryLoad<PlayerSave>("player_1", out var data))
    UnityEngine.Debug.Log(data.coins);
```

Dùng field serializable theo quy tắc `JsonUtility`; không dùng Dictionary, property hoặc
Unity object reference cho save game. Dữ liệu ở `Application.persistentDataPath/BaseUnity`.
Slot chỉ gồm chữ, số, `_`, `-`; tối đa 64 ký tự. Ghi file tạm rồi replace và giữ thế hệ trước
ở `.bak`; khi file chính hỏng sẽ thử backup. Lỗi ghi được đưa cho caller xử lý.

Version mặc định là 1. Để migration, gọi `TryLoad<OldData>(slot, out old, oldVersion)`,
chuyển đổi rồi `Save(slot, newData, newVersion)`. Thư viện không tự suy đoán schema migration.
Save không mã hóa và không chống sửa dữ liệu. File persistence hiện kiểm thử trên Windows;
WebGL cần adapter lưu trữ/sync riêng, chưa được hỗ trợ bởi `JsonSaveStore` v0.1.

## Scene, UI và audio

- `await SceneService.LoadAsync("Gameplay")`: scene phải có trong Build Profiles scene list.
  Có mode additive, progress và CancellationToken. Unity không hủy native load đã bắt đầu:
  task chỉ báo hủy **sau khi load hoàn tất**, scene vẫn được load. Chỉ chạy một scene operation tại một thời điểm.
  `UnloadAsync` dùng cho scene additive, không unload scene cuối cùng.
- `UIPanel`: gắn lên root screen/popup. `UIService.ShowScreen(panel)`, `PushPopup(panel)`,
  `Back()` đóng popup trên cùng. Các popup cần chung parent Canvas để sibling order đúng.
  Thiết kế Canvas, raycast blocker và animation do game quyết định. Khi mở scene, ẩn panel không dùng;
  UIService không tự tìm và ẩn các panel chưa đăng ký.
- `AudioService.PlayMusic(clip)`, `PlayEffect(clip)`, `SetVolume(master, music, effects, muted)`.
  Gọi `SaveSettings()` khi người chơi xác nhận thay đổi. Bản đầu hỗ trợ âm thanh 2D,
  volume/mute; mixer routing, crossfade và giới hạn voice chưa có.

## Input

Mặc định dùng `ManualInputSource`: UI button/joystick gọi `SetButton`, `SetAxis`, `SetVector2`.
Game đọc qua `InputRouter`; `Enabled = false` chặn kết quả input, không pause thời gian game.
Reset source khi đóng touch UI để tránh giữ phím ảo. Bootstrap reset manual input khi mất focus.

Nếu cài `com.unity.inputsystem`, assembly adapter tự được bật. Ví dụ trong lớp bootstrap kế thừa:

```csharp
[UnityEngine.SerializeField] private UnityEngine.InputSystem.InputActionAsset actions;

protected override BaseUnity.Input.IInputSource CreateInputSource(BaseUnity.Input.ManualInputSource fallback)
{
    var source = new BaseUnity.Input.InputSystemSource(actions, "Gameplay");
    Services.Register(source); // Registry quản lý dispose asset clone.
    return source;
}
```

`InputSystemSource.SwitchMap("UI")` chuyển ngữ cảnh. Dùng tên đầy đủ như `Gameplay/Move`.
`ReadAxis` yêu cầu action float, `ReadVector2` yêu cầu action Vector2. Binding keyboard,
gamepad, touch và rebind UI do dự án định nghĩa trong Input Actions asset.
Game có asmdef riêng phải reference `BaseUnity.InputSystem` nếu dùng adapter này.
Nếu không cài Input System, core và manual input vẫn hoạt động.

## Pool, timer và log

```csharp
var pool = new BaseUnity.Pooling.PrefabPool(prefab, ownerTransform, prewarm: 8, maxRetained: 32);
var instance = pool.Rent(position, rotation);
pool.Return(instance);
pool.Dispose(); // Hủy cả object đang được thuê; gọi khi owner kết thúc.
```

`maxRetained` giới hạn số object nhàn rỗi, không giới hạn số object đang dùng.
Không destroy trực tiếp object đang thuê. Reset trạng thái gameplay qua `OnEnable`/`OnDisable`
hoặc script của game. Pool không tự reset Rigidbody, particle hay dữ liệu component.

`TimerService.Schedule(seconds, callback, unscaled: true, repeat: true)` trả handle `IDisposable`
để hủy. Timer lặp chạy tối đa một lần mỗi frame, bỏ qua số lần trễ khi frame bị chậm;
timer được tạo trong callback bắt đầu xử lý từ tick kế tiếp. Hủy handle trong `OnDisable`/`OnDestroy`
đối với timer thuộc scene. Bootstrap tự tick timer và state machine.

`GameLog.Info/Warning/Error(category, message)`; điều chỉnh `GameLog.MinimumLevel` khi khởi động
để lọc log, đặt `Off` để tắt. Không tự tắt log trong release build.

## Ví dụ và kiểm thử

Với submodule, copy `Samples~/QuickStart/QuickStartBootstrap.cs` vào thư mục scripts của game,
gắn component vào root scene thay bootstrap mặc định. Với UPM, import sample qua Package Manager.
Chạy sample sẽ đọc save, sau 1 giây cộng 1 coin, lưu và phát event.

Test Runner: chạy assembly `BaseUnity.Tests` ở EditMode và `BaseUnity.PlayModeTests` ở PlayMode.
Game cần Unity Test Framework. Nếu cài qua UPM, thêm `"com.baseunity.foundation"` vào
`testables` trong `Packages/manifest.json` của game để hiện package tests.
Tests không vào bản build thông thường. Không bật test assemblies cho bản phát hành.

Đã kiểm thử v0.1 trên Unity 6000.3.26f1/Windows: **20 EditMode + 5 PlayMode tests đều pass**.
Đã kiểm tra compile với Input System 1.20.0 và chạy core tests khi không cài Input System.
Sample cũng được import để kiểm tra compile. Chưa kiểm thử player build trên Android/iOS/WebGL.

V0.2 được kiểm tra trực tiếp qua Unity MCP trong project BaseUnity: **6 PlayMode tests pass**
(5 core + 1 bài test toàn bộ game flow). Bài test flow được chạy lại sau chỉnh sửa UI cuối cùng và pass.
Đã kiểm tra Home trong Game View và xác nhận chạy lại generator không đổi scene/config có sẵn.

V0.4: **11/11 PlayMode tests pass** trong project BaseUnity qua Unity MCP, gồm cả mở khóa level,
Next/Retry, timer khi pause, kết thúc lặp, màn cuối, lưu qua restart và save cũ.

V0.5: **15/15 PlayMode tests pass**, bổ sung vòng đời prefab, callback từ màn cũ,
phân trang/chọn màn khóa, tương tác mục tiêu/bẫy và UI mặc định không có Layer Lab.
Đã kiểm tra trực quan Game View dọc và xác nhận raycast chạm đúng mục tiêu gameplay.

V0.6: **26 EditMode + 15 PlayMode tests pass** khi không cài SDK quảng cáo/Firebase.
Test dùng backend giả để kiểm tra công tắc, provider thiếu, cooldown, kết quả thưởng và callback đến muộn.
Adapter SDK riêng chưa được compile với binary của nhà cung cấp hoặc kiểm thử trên Android/iOS;
cần cài SDK và cấu hình tài khoản trong dự án game để kiểm tra tích hợp thật.

Trong workspace phát triển ban đầu, `Assets/BaseUnity/Runtime`, `Editor`, `Tests` là junction
đến source ở root, giúp Editor import trực tiếp mà Git vẫn chỉ lưu thư viện.
`Assets/BaseUnitySamples` chứa bản copy sample để thử trong Editor. Các đường dẫn sandbox này
không đi lên Git; khi clone thư viện ở máy khác, hãy import vào một dự án Unity theo hướng dẫn ở trên.

Phạm vi v0.1: foundation nhỏ, dùng được và có API mở rộng; chưa gồm Addressables, inventory,
SDK online, cloud save, IAP, ads, localization hay multiplayer.

V0.7: **49/49 EditMode + 18/18 PlayMode tests pass**. Bổ sung kiểm thử economy/localization và PlayMode cho menu, mua/nhận quà, win/lose/next,
callback thưởng và thử lưu lại. SDK đã compile trong workspace với Firebase 14.0.0, MAX 8.6.6 và adapter
Google qua MAX. Chưa kiểm thử quảng cáo/Firebase thật trên thiết bị vì còn thiếu app config và IDs tài khoản.
Firebase Editor thiếu file app config sẽ trả trạng thái Failed/LastError để game offline tiếp tục chạy.

V0.8: **60/60 EditMode + 19/19 PlayMode tests pass**: migration một lần, giao dịch tiến trình/thưởng,
backup và save hỏng, chặn schema tương lai, lỗi ghi tiền thưởng, khôi phục Result qua restart,
prefab menu giữ instance, trạng thái tab và animation đóng khi timeScale = 0.

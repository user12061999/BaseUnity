# BaseUnity Foundation

Base game Unity tái sử dụng qua **Git submodule trong `Assets/BaseUnity`**.
Repository chứa thư viện, không chứa nguyên dự án Unity. Namespace: `BaseUnity`.
Phiên bản hiện tại: **0.3.0**, dành cho Unity **6.0 trở lên**; môi trường kiểm thử: Unity **6000.3.26f1**, Windows.

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
Bố cục dành cho game landscape, tự fit trong safe area; chưa có bố cục portrait riêng.

## Cài vào dự án khác

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

Trong workspace phát triển ban đầu, `Assets/BaseUnity/Runtime`, `Editor`, `Tests` là junction
đến source ở root, giúp Editor import trực tiếp mà Git vẫn chỉ lưu thư viện.
`Assets/BaseUnitySamples` chứa bản copy sample để thử trong Editor. Các đường dẫn sandbox này
không đi lên Git; khi clone thư viện ở máy khác, hãy import vào một dự án Unity theo hướng dẫn ở trên.

Phạm vi v0.1: foundation nhỏ, dùng được và có API mở rộng; chưa gồm Addressables, inventory,
SDK online, cloud save, IAP, ads, localization hay multiplayer.

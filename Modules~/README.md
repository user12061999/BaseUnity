# BaseUnity selective modules — v0.24

Các module cài riêng: **Core, Save, Audio, Pooling, MobileInput, GameFlow, UI** và bridge tùy chọn **GameFlow UI**.
Module Manager chỉ đóng gói file trong catalog thành package/assembly riêng. Economy/Progression/Ads/Firebase và các nhóm còn lại vẫn ở legacy, không bị kéo vào các module mới.

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

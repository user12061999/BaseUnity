# BaseUnity selective modules — v0.23

Đây là đợt tách đầu tiên: **Core, Save, Audio, Pooling, MobileInput**. Source trong repository là nguồn chuẩn;
Module Manager chỉ đóng gói/cài các file trong `catalog.json`. Mỗi module có package UPM và assembly riêng.
GameFlow/UI/Economy/Progression/Rewards/Energy/Ads/Firebase/Localization của v0.22 vẫn ở bản legacy,
chưa quảng cáo là những package có thể cài riêng. Chúng không bị kéo vào năm module mới.

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
Đợt sau sẽ tách GameFlow và những nhóm còn lại, rồi bổ sung migration cụ thể cho prefab/config cũ.

## Kiểm chứng trong project sạch

Đã chạy Unity 6000.3.26f1 batchmode với manifest ban đầu không có BaseUnity:
Core-only tạo đúng một assembly BaseUnity.Core; Core+MobileInput tạo đúng hai assembly;
chọn cả năm tạo đúng năm assembly. Cả ba không có BaseUnity.Features.FeatureServices.
Module Manager cũng biên dịch trong project sạch mà không tham chiếu bundle cũ.

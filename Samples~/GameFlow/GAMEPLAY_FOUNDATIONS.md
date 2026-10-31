# Gameplay foundations (v0.21)

## Bật và cấu hình

Chọn FeatureConfig ở **Tools > BaseUnity > Feature Manager > Modules**:

- **Gameplay foundations**: master toggle. Bên trong có energy, progression, gifts, badges, effects, interactions, mobileControls. Trường chi tiết chỉ hiện khi nhóm tương ứng bật.
- **Advanced player settings**: FPS mặc định và FPS tiết kiệm pin.
- **Offline > Achievements**: dùng lại danh mục và logic thành tích đã có; bật Offline để nhận thưởng thành tích. Shop/Daily/Missions vẫn có cờ riêng.

Dự án cũ mặc định tắt các nhóm mới. Năng lượng/EXP/quà không yêu cầu bật cửa hàng offline.
Cấu hình được sao chép lúc bootstrap; dừng Play rồi chỉnh để áp dụng lần khởi động sau.

Chọn GameFlowConfig và chạy **Tools > BaseUnity > Game Flow > Prepare Gameplay Foundations**.
Generator tạo asset riêng trong `GameplayFoundations`, gán FoundationMenu vào config và giữ asset đã tồn tại.
Setup Wizard/Generate Editable UI Prefabs cũng gọi generator. Có backup trước khi gán/chỉnh cấu hình.

## Các chức năng

| Nhóm | Hành vi |
|---|---|
| Hiệu ứng | Chữ nổi cộng điểm, chữ/vật phẩm dạng text bay về vị trí HUD (`GameplayEffects.Show` với destination), flash/punch (`GameplayHitFeedback`). Pool có giới hạn số effect đồng thời. |
| Tương tác | Tap, chọn/bỏ chọn qua selection object, collect một lần mỗi lần kích hoạt, kéo UI trong giới hạn parent, drop zone chỉ nhận một lần cho mỗi lần kéo. Có UnityEvent để game nối logic. |
| Cài đặt | FPS 30/60/120, quality theo danh sách Unity, battery saver, reduced motion, bật/tắt gameplay effect; rung dùng HapticService hiện có. Save thành công rồi mới cập nhật preference trong bộ nhớ. |
| Thông báo | Badge theo ID, xóa badge khi hết phần thưởng; toast dùng FeedbackService hiện có. Có toast khi tăng cấp người chơi. Không phải push notification của hệ điều hành. |
| Năng lượng | Dung lượng, giá mỗi lượt, thời gian hồi; hồi khi mở lại game, không vượt giới hạn. Chỉ trừ khi thực sự vào Playing; load lỗi hoặc hủy ở countdown chưa trừ. Restart vào Playing tính là lượt mới. Resume không trừ thêm. |
| Tiến trình | EXP thắng/thua, ngưỡng EXP cố định mỗi cấp, cấp tối đa, thưởng coin khi tăng cấp. Ghi cùng kết quả màn, không cộng lại khi retry lưu kết quả. |
| Thành tích | Dùng lại Offline achievement goals; menu mới hiển thị tiến độ, trạng thái đã nhận và phần thưởng; lưu cùng wallet. |
| Quà tặng | Welcome nhận một lần, Return khi quay lại sau khoảng thời gian cấu hình, Playtime theo thời gian Playing ở foreground, Periodic theo cooldown. |
| Điều khiển | Joystick ghi vector Move; nút giữ ghi Fire; swipe/tap phát UnityEvent. Mỗi control giữ một pointer riêng, nhả input khi bị tắt hoặc game không còn Playing. |

Reduced motion tắt chuyển động chữ/punch; chữ vẫn có thể fade. GameplayEffects preference tắt hiệu ứng gameplay.
UI feedback hiện có cũng nhận preference reduced motion. Tắt tính năng không xóa dữ liệu đã lưu.

## Prefab mẫu

Các đường dẫn dưới đây tính từ thư mục chứa GameFlowConfig, ví dụ `Assets/BaseUnityGameFlow`.

| Prefab trong GameplayFoundations | Công dụng |
|---|---|
| FoundationMenu.prefab | Canvas gốc: nút Gifts/Profile, năng lượng/XP, tab quà/thành tích/cài đặt, effect layer. |
| FloatingText.prefab | Template chữ nổi cho pool. |
| NotificationBadge.prefab | Badge độc lập, chọn badgeId như gifts hoặc achievements. |
| Joystick.prefab | Joystick mobile, action mặc định Move. |
| HoldButton.prefab | Nút giữ, action mặc định Fire. |
| SwipeSurface.prefab | Vùng nhận swipe và tap. |
| TapObject.prefab | Đối tượng chạm, có hit feedback. |
| Collectible.prefab | Vật phẩm thu thập một lần mỗi activation. |
| Draggable.prefab | Đối tượng kéo UI. |
| DropZone.prefab | Vùng nhận đối tượng kéo. |
| GameplayPlayground.prefab | Gameplay dọc ghép các control/đối tượng trên, di chuyển actor, cộng điểm. |

`GameplayDemoLevel.asset` là LevelData để thử playground. Gán vào một entry trong GameFlowConfig > Levels > Data
để chơi bằng Bootstrap; generator không thay các level đang dùng. Bật gameplay foundations và các nhóm tương ứng
để tương tác hoạt động. Playground tận dụng EventSystem/HUD/Pause/Win/Lose của GameFlow.
Các nút/demo component là ví dụ để chỉnh; chưa áp đặt cách di chuyển, combat hay luật riêng của game.
Tap/collect có thể dùng với world object nếu camera có PhysicsRaycaster/Physics2DRaycaster và collider phù hợp;
phần drag mẫu làm việc trên RectTransform trong Canvas.

## API nhanh

Lấy service qua `bootstrap.Services.Get<FeatureServices>()`:

```csharp
var modules = bootstrap.Services.Get<FeatureServices>();
int energy = modules.Gameplay.Energy;
int level = modules.Gameplay.PlayerLevel;
long xp = modules.Gameplay.Experience;
bool claimed = modules.Gameplay.Claim("welcome");
modules.Notices.Set("inventory", 3);
// Chỉ gọi sau khi gameplay/SDK đã xác nhận phần thưởng tương ứng.
modules.Gameplay.AddEnergy(1);
```

GameFlow tự kiểm tra/trừ năng lượng, ghi EXP cùng kết quả và tick playtime. Khi dùng GameBootstrap riêng,
game gọi SpendRunEnergy lúc bắt đầu thực sự, TickPlaytime(delta) trong gameplay foreground,
Visit lúc vào/tiếp tục ứng dụng và Heartbeat định kỳ/khi ra nền. FinishSession của OfflineService tự ghi EXP.
Không gọi SpendRunEnergy thêm lần nữa nếu đã dùng GameFlow.

Joystick/HoldButton ghi vào ManualInputSource; nếu bootstrap thay CreateInputSource bằng adapter khác,
phải kết hợp nguồn manual với adapter đó hoặc tự đọc manual. Swipe và interaction dùng UnityEvent.
GameplayInputGate kiểm tra InputRouter.Enabled và cờ module; game riêng phải cập nhật router theo trạng thái chơi.
Effect vị trí nhận screen coordinates; truyền destination để bay về HUD.

## Lưu trữ và giới hạn

Energy, EXP, giờ chơi, mốc quà, coin và trạng thái đã nhận nằm trong cùng Offline profile schema **5**.
Profile schema cũ 0–4 được bổ sung dữ liệu mặc định, giữ wallet/tiến độ; lần ghi tiếp theo nâng lên schema 5.
Bản base cũ chỉ hiểu schema 4 sẽ từ chối profile mới: backup dữ liệu trước khi downgrade.
Advanced preferences dùng slot riêng `baseunity_advanced_settings`; rung giữ slot cũ của HapticService.

Thời gian dùng UTC cục bộ, bảo vệ đơn giản khi đồng hồ đi lùi bằng anchor đã lưu. Đây không phải chống gian lận
thời gian máy: game online nên thay bằng nguồn giờ tin cậy. Giờ chơi được flush mỗi 30 giây và khi app ra nền;
crash/force-kill có thể mất tối đa phần chưa flush. Phần thưởng và dấu đã nhận vẫn ghi nguyên tử trước khi công bố.
Level coin bonus theo cấp cũng ghi trong transaction kết quả. Save thất bại không tiêu năng lượng/nhận quà ảo.

Quality/FPS cần thử trên thiết bị mục tiêu; mức 120 FPS là giới hạn yêu cầu, không bảo đảm phần cứng đạt được.
Các sample UI hiện dùng theme/font của dự án và có thể chỉnh bằng Prefab Mode.

# Danh mục 42 prefab mẫu

Các đường dẫn bên dưới tính từ thư mục chứa GameFlowConfig. Trong project BaseUnity hiện tại,
thư mục này là `Assets/BaseUnityGameFlow`.

Bộ mẫu được nối vào config; chạy scene Bootstrap để xem toàn bộ luồng. Để chỉnh giao diện,
mở prefab bằng Prefab Mode. Mỗi game có thể giữ các presenter và thay sprite, font, màu, layout.
Theme chung nằm ở `UI/Theme/Theme.asset`; nhạc nằm ở `UI/Theme/AudioProfile.asset`.

Các prefab gốc được bootstrap/presenter tạo tự động. Screens, card, cell, row và template
được prefab gốc tham chiếu; khi dùng luồng có sẵn, không cần kéo từng prefab con vào scene.
Nếu tự ghép scene, cần giữ presenter, reference và EventSystem phù hợp.

Để chuẩn bị bộ mẫu ở dự án khác sau khi cài sample, chọn GameFlowConfig rồi chạy:

1. **Tools > BaseUnity > Game Flow > Generate Editable UI Prefabs**.
2. **Tools > BaseUnity > Game Flow > Generate Level Assets**.
3. **Tools > BaseUnity > UI > Prepare Theme and Shared Components**.

Bước 1 cũng chuẩn bị SessionOverlay và AudioProfile. Các generator giữ asset đã tồn tại;
công cụ theme/session có backup trước khi chỉnh. Nội dung Layer Lab/font phải có trong dự án
nếu prefab đang tham chiếu đến chúng; repository phân phối code generator, không đóng gói asset pack bên ngoài.

## Prefab gốc

| Prefab | Công dụng |
|---|---|
| `UI/Theme/GameFlow.prefab` | Canvas chính: Home cơ bản, InGame HUD, Loading, Settings, Pause, Result và Error. Pause có nút Restart được gán trên prefab gốc. |
| `UI/Theme/HomeHub.prefab` | Home đầy đủ với nút Play, Store, Quest, Daily Reward, Inventory và menu tính năng; các nút hiển thị theo cấu hình. |
| `UI/Theme/MobileModules.prefab` | Presenter UI cho menu, popup hàng đợi, tutorial, toast và loading indicator. |
| `UI/Theme/OfflineOverlay.prefab` | Presenter UI cho thanh tiền Home, phần thưởng kết quả và chờ quảng cáo. |

## Màn hình và popup

| Prefab | Công dụng |
|---|---|
| `UI/Theme/Screens/Home.prefab` | Home cơ bản, nút Play và chọn level. |
| `UI/Theme/Screens/HomeHub.prefab` | Phần màn hình Home của HomeHub gốc. |
| `UI/Theme/Screens/InGame.prefab` | HUD điểm, mục tiêu, timer và nút Pause. |
| `UI/Theme/Screens/LevelSelection.prefab` | Chọn level, khóa/mở khóa và phân trang. |
| `UI/Theme/Screens/Loading.prefab` | Màn hình tải scene với thanh tiến trình. |
| `UI/Theme/Screens/Settings.prefab` | Âm lượng Master/Music/Effects, mute và quyền riêng tư. |
| `UI/Theme/Screens/Pause.prefab` | Nền menu tạm dừng và các nút Resume/Home. Nút Restart được thêm qua override trên GameFlow.prefab ở dự án đã nâng cấp. |
| `UI/Theme/Screens/Result.prefab` | Một prefab kết quả dùng cho cả Win và Lose, Retry/Home/Next. |
| `UI/Theme/Screens/Error.prefab` | Thông báo lỗi và trở về Home. |
| `UI/Theme/Screens/SessionOverlay.prefab` | Canvas hiển thị chuẩn bị, đếm ngược và kết thúc; có thể chỉnh các thông báo trong Inspector. |
| `UI/Theme/Screens/PurchaseConfirmation.prefab` | Xác nhận mua vật phẩm. |
| `UI/Theme/Screens/QueuedPopup.prefab` | Hộp thoại với tiêu đề, nội dung, Accept/Cancel. |
| `UI/Theme/Screens/Tutorial.prefab` | Hướng dẫn với highlight/mask và nút Next/Skip. |
| `UI/Theme/Screens/Toast.prefab` | Thông báo ngắn. |
| `UI/Theme/Screens/MoreMenu.prefab` | Menu tính năng bổ sung, hiển thị theo cấu hình. |
| `UI/Theme/Screens/LoadingIndicator.prefab` | Thông báo tác vụ đang chạy. |
| `UI/Theme/Screens/WaitingForAd.prefab` | Overlay chờ quảng cáo. |
| `UI/Theme/Screens/OfflineHomeBar.prefab` | Thanh tiền và nút menu ở Home. |
| `UI/Theme/Screens/ResultReward.prefab` | Phần thưởng kết quả, nhân đôi reward và thử lưu lại khi lỗi. |

## Store, Quest và tính năng offline

| Prefab | Công dụng |
|---|---|
| `HomeHub/Store.prefab` | Trang cửa hàng dạng card, nối dữ liệu Shop/IAP theo feature config. |
| `HomeHub/Quests.prefab` | Trang nhiệm vụ/thành tựu dạng card và nhận thưởng. |
| `OfflineUI/OfflineMenu.prefab` | Menu gốc chứa các tab Shop/Daily Reward/Missions/Inventory. |
| `OfflineUI/Shop.prefab` | Trang cửa hàng offline dạng danh sách. |
| `OfflineUI/DailyReward.prefab` | Trang nhận thưởng hằng ngày. |
| `OfflineUI/Missions.prefab` | Trang nhiệm vụ dạng danh sách. |
| `OfflineUI/Inventory.prefab` | Trang túi đồ. |
| `OfflineUI/ItemRow.prefab` | Template dòng vật phẩm/nhiệm vụ trong các trang offline. |

## Component dùng chung

| Prefab | Công dụng |
|---|---|
| `UI/Theme/Shared/PrimaryButton.prefab` | Nút hành động chính. |
| `UI/Theme/Shared/SecondaryButton.prefab` | Nút phụ. |
| `UI/Theme/Shared/TabButton.prefab` | Nút chuyển tab. |
| `UI/Theme/Shared/CurrencyBar.prefab` | Thanh hiển thị tiền. |
| `UI/Theme/Shared/ItemCard.prefab` | Card vật phẩm cửa hàng. |
| `UI/Theme/Shared/QuestCard.prefab` | Card nhiệm vụ và tiến trình. |
| `UI/Theme/Components/LevelCell.prefab` | Template ô chọn level. |
| `UI/Theme/Components/MenuButton.prefab` | Template nút menu động. |
| `UI/Theme/Components/MenuLabel.prefab` | Template nhãn trong menu. |
| `UI/Theme/Components/Panel.prefab` | Template panel dùng trong menu. |

## Gameplay mẫu

| Prefab | Công dụng |
|---|---|
| `Levels/TapTargetGameplay.prefab` | Gameplay màn dọc: chạm mục tiêu để thêm điểm, tránh bẫy; nối LevelContext và Win/Lose. |

Save và AudioService là service runtime; AudioProfile là ScriptableObject cấu hình, không phải prefab.

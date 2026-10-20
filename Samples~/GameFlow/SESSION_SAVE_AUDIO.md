# Save, audio và vòng đời InGame (v0.20)

## Cấu hình mẫu

Chạy **Tools > BaseUnity > Game Flow > Prepare Session Flow and Audio** sau khi cập nhật sample.
Generator tạo asset trong thư mục của GameFlowConfig: `UI/Theme/AudioProfile.asset` và
`UI/Theme/Screens/SessionOverlay.prefab`; thêm nút Restart vào Pause nếu còn thiếu.
Trước khi gán reference/chỉnh prefab, công cụ backup vào `ProjectSettings/BaseUnityUIBackups`.
Chạy lại giữ nguyên profile, prefab và nút đã có. Setup Wizard cũng chuẩn bị chúng khi sinh UI prefab.

Bật `GameFlowConfig.sessionFlow.enabled`, chỉnh `prepareSeconds`, `countdownSeconds`,
`resultDelaySeconds`. Mặc định tắt để các dự án cũ giữ hành vi hiện có; giá trị 0 bỏ qua giai đoạn đó.
Mở SessionOverlay bằng Prefab Mode để chỉnh màu, font, nội dung và bố cục, hoặc đổi reference sang prefab của game.
Theme preparation/application có xử lý prefab này.

## Luồng phiên chơi

`Loading → Preparing → Countdown → Playing → Finishing → Result`.

- Gameplay được tạo/Initialize trong Loading; `LevelContext.IsPlaying` chỉ true trong Playing.
- Dùng InputRouter/LevelContext cho input và kết quả. Script game đọc input trực tiếp cần tự kiểm tra Phase.
- Preparing/Countdown/Finishing dừng scaled time, khóa router và UI gameplay. Stage chạy bằng unscaled time.
- Mất focus hoặc ứng dụng pause sẽ giữ nguyên thời gian stage. Nếu đang Playing, chuyển sang Paused và yêu cầu Resume.
- Khi vào Playing mới phát `SessionStarted`. Pause/Resume không phát lại sự kiện này.
- Win/Lose khóa kết quả ngay, ghi tiến độ/reward một lần rồi phát `SessionEnded` tại lúc kết thúc gameplay.
  Nếu bật delay, sự kiện này đến trước Result; UI kết quả chỉ hiện sau delay. Nút next/retry/home chờ Result.
- `RestartLevel()` dùng từ Preparing/Countdown/Playing/Paused: bỏ lượt hiện tại, dọn prefab/context cũ,
  reset điểm/timer, tạo lại màn. Không tạo kết quả hoặc reward cho lượt bị bỏ. Lệnh trong lúc loading bị bỏ qua.
- Input manual được reset khi đổi phase để không giữ lại touch/phím của phiên trước.

## Audio

AudioProfile có cờ bật/tắt, clip Home/InGame/Win/Lose, thời gian crossfade và policy SFX.
Nếu chưa gán Home/InGame và bật `demoMusic`, mẫu phát loop tổng hợp nhỏ do code tạo; tắt demo hoặc gán clip riêng.
Loading/Pause/Finishing giữ nhạc hiện tại; Result phát clip thắng/thua nếu có, nếu không dùng nhạc Home.
Cấu hình có hiệu lực khi khởi động; thay clip/profile rồi Play lại. Âm lượng Master/Music/Effects/Mute dùng settings hiện có.

`AudioService.CrossFadeMusic(clip, seconds)` chuyển bằng hai nguồn nhạc. Có thể đảo chiều/đổi đích khi đang fade.
`FadeOutMusic` dừng và nhả clip khi xong; `PlayMusic` giữ API cũ và chuyển ngay.
`GameBootstrap.Update` tự gọi `Tick(Time.unscaledDeltaTime)`; nếu tạo service độc lập phải tự gọi Tick.
`TryPlayEffect` trả false nếu mute, trùng quá nhanh hoặc đạt giới hạn đồng thời; không tạo voice vượt policy.
`ConfigureEffects(totalVoices, perClip, cooldownSeconds)` áp dụng cả âm thanh UI/gameplay sử dụng cùng service.
Service không destroy AudioClip do dự án sở hữu.

## Save có phiên bản

`ISaveStore` giữ API cũ. `JsonSaveStore` đọc envelope cũ không có checksum; các lần ghi mới có SHA-256 để phát hiện hỏng dữ liệu.
Checksum không phải cơ chế chống gian lận hay mã hóa. Việc ghi vẫn dùng file tạm và replace nguyên tử; có một thế hệ `.bak`.

Registry riêng cho mỗi store, mỗi bước tăng đúng 1 version. Đăng ký toàn bộ đường đi trước khi load:

```csharp
[Serializable]
public sealed class MyProgress { public int coins; public string displayName; }

var migrations = new SaveMigrationRegistry();
migrations.Register<MyProgress>(1, json => {
    var data = JsonUtility.FromJson<MyProgress>(json);
    data.displayName = "Player";
    return JsonUtility.ToJson(data);
});
var store = new JsonSaveStore(Application.persistentDataPath, migrations);
var state = store.LoadVersioned<MyProgress>("progress", out var data, 2,
    value => value.coins >= 0);
// Sau khi load thành công, mọi lần ghi phải dùng cùng version hiện tại.
if (state != SaveLoadState.Missing) store.Save("progress", data, 2);
```

Trong bootstrap kế thừa `GameBootstrap`, override `ConfigureSaveMigrations(registry)` để đăng ký
trước khi Audio/Features load dữ liệu. GameFlowBootstrap mẫu là sealed: game dùng store registry từ
`((JsonSaveStore)flow.Services.Get<ISaveStore>()).Migrations` trước khi load các slot riêng,
hoặc tạo bootstrap game kế thừa GameBootstrap. Không tăng version/schema các slot mẫu nếu chưa viết migration tương ứng.
Profile offline vẫn giữ migration schema hiện có; envelope version của nó vẫn là 1.

Transform phải thuần JSON, không ghi file/phát event/grant reward. LoadVersioned kiểm tra đủ bước,
chạy migration trong bộ nhớ, validate rồi mới ghi. Thiếu bước, lỗi transform/validation hoặc gặp version mới hơn
sẽ báo lỗi và giữ file. Có thể `persistMigration: false` để xem thử trong bộ nhớ.
TryLoad/LoadValidated có thể chạy migration đã đăng ký nhưng không tự lưu; dùng LoadVersioned cho luồng upgrade đầy đủ.
TryLoad trả false khi version mới hơn; LoadValidated/LoadVersioned báo lỗi. Save từ bản cũ từ chối đè file version mới.

Main hỏng/checksum sai có thể phục hồi backup hợp lệ. Lần ghi kế tiếp giữ backup tốt và lưu main hỏng dưới
`.corrupt-<id>` để chẩn đoán. `Inspect(slot)` đọc trạng thái envelope/checksum/version mà không sửa file;
`RestoreBackup<T>(slot, validate, version)` khôi phục có kiểm tra và lưu main trước đó để chẩn đoán.
Inspect không thay validation nội dung ứng dụng. Delete xóa main/bak/tmp; file chẩn đoán được giữ để developer tự quản lý.
Tất cả API save dùng trên main thread; không gọi đồng thời cho cùng slot.

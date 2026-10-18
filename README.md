# BaseUnity Foundation

Base game Unity tái sử dụng qua **Git submodule trong `Assets/BaseUnity`**.
Repository chứa thư viện, không chứa nguyên dự án Unity. Namespace: `BaseUnity`.
Phiên bản hiện tại: **0.13.0**, dành cho Unity **6.0 trở lên**; môi trường kiểm thử: Unity **6000.3.26f1**, Windows.

## Luồng game có sẵn

Chọn **Tools > BaseUnity > Setup and Update Wizard**, chọn module, scan/review file rồi bấm
**Apply selected files and continue Setup**. Chờ compile/Setup hoàn tất, mở
`Assets/BaseUnityGameFlow/Scenes/Bootstrap.unity` và nhấn Play. Menu **Install Game Flow Sample** cũ cũng mở Wizard.
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

- Hồ sơ offline dùng `schemaVersion = 3` bên trong payload; envelope `ISaveStore` vẫn ở version 1 để giữ tương thích API.
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

## Kiểm tra tự động trước build (0.9)

Mặc định bật **Validate before build** tại **Project Settings > BaseUnity > Build Validation**.
Có thể mở nhanh bằng **Feature Manager > Setup > Automatic build validation settings**.

| Thiết lập | Hành vi |
| --- | --- |
| Validate before build | Đọc scene/config và chặn build nếu có error |
| Block test ads in Release | Chặn Ads test mode trong build không có cờ Development; chỉ áp dụng khi Ads và ít nhất một định dạng được bật |
| Treat warnings as errors | Khi bật, warning cũng chặn build; mặc định tắt |

Bộ kiểm tra lấy **scene list của yêu cầu build**, hỗ trợ danh sách scene riêng của Build Profile hoặc
`BuildPipeline.BuildPlayer`. Chỉ kiểm tra FeatureConfig được tham chiếu bởi Bootstrap trong các scene đó;
config demo hoặc config cũ không dùng sẽ không cản build. Với Game Flow, `GameFlowConfig.features` có ưu tiên
so với field FeatureConfig trên Bootstrap, giống hành vi runtime. Scene Bootstrap phải đứng đầu danh sách.

Các lỗi được kiểm tra gồm scene thiếu, missing script, Bootstrap không nằm ở root, config Game Flow chưa gắn,
level/slot lưu không hợp lệ, binding UI thiếu và các kiểm tra Ads/Firebase/offline/localization hiện có.
Nhiều Bootstrap được đưa vào cùng build tạo warning. Module tắt bỏ qua thông tin SDK/ID không còn dùng.
Quảng cáo test vẫn dùng được trong Development Build khi các điều kiện cấu hình khác hợp lệ.

Nếu có lỗi, build dừng với `BuildFailedException` và hướng dẫn trên Console. Báo cáo đầy đủ ở
`Library/BaseUnity/LastBuildValidation.json`, gồm platform, Development/Release, scene list, mức lỗi và asset liên quan;
không chép SDK key hay nội dung file Firebase vào báo cáo. Có nút **Show last automatic report** trong thiết lập.
Thiết lập được lưu riêng theo game tại `ProjectSettings/BaseUnityBuildValidation.asset`; không nằm trong submodule.

Callback dùng [BuildPlayerProcessor.PrepareForBuild](https://docs.unity.cn/6000.0/Documentation/ScriptReference/Build.BuildPlayerProcessor.PrepareForBuild.html)
và [BuildPlayerContext.BuildPlayerOptions](https://docs.unity.cn/6000.0/Documentation/ScriptReference/Build.BuildPlayerContext.html).
Các scene được đọc trong preview rồi đóng, không save hoặc thay scene đang mở, không khởi tạo SDK.
Build từ command line qua `BuildPipeline.BuildPlayer` cũng nhận kiểm tra này.

Công cụ kiểm tra các tham chiếu được serialize trên `GameBootstrap` và sample `GameFlowBootstrap`.
Nếu game override `GetFeatureConfig` để tạo config động hoặc dùng hệ thống scene/addressable riêng,
phần cấu hình đó cần kiểm tra riêng. Cửa sổ **Validate Build Configuration** cũ vẫn là kiểm tra thủ công theo
Editor scene list; báo cáo tự động mới dùng scene list của đúng lần build.
Không thay thế việc kiểm thử APK/device, vendor dashboard hoặc xác nhận cấu hình đã áp dụng vào SDK.

## Setup Wizard và cập nhật sample (0.11)

Mở **Tools > BaseUnity > Setup and Update Wizard**, hoặc từ **Feature Manager > Setup**.
Thư mục mặc định là `Assets/BaseUnityGameFlow`. Mỗi project có một bản cài code Game Flow để tránh trùng assembly;
khi đã cài ở thư mục khác, Wizard tự chọn thư mục đó. Đích phải nằm trong Assets, không dùng junction/symlink.

### Cài mới / cấu hình

1. Ở tab **Setup**, chọn Wallet/Shop/Daily/Missions/Rewards, localization, Ads/Firebase, tạo LevelData/UI và UniTask tùy chọn.
2. Bấm **Scan source files / review update** để xem các file sẽ thêm hoặc cập nhật.
3. Kiểm tra diff/lựa chọn, bấm **Apply selected files and continue Setup**. Tác vụ được lưu và tự tiếp tục sau compile/reload.
4. Xem **Setup state**. Wizard tạo các scene/config/prefab còn thiếu và kiểm tra cấu hình sau khi tạo.

Với bản cài đã có, lựa chọn module được đọc từ config hiện tại. Chỉ đổi công tắc đã chọn; IDs, provider, giá shop,
tiền thưởng, bản dịch và prefab/scene đã chỉnh sửa được giữ. Config dùng chung ngoài thư mục sample phải cấu hình
qua Feature Manager thay vì để Wizard sửa. Scene/config/prefab có sẵn không bị generator ghi đè.
Các builder chỉ lưu asset do bước Setup sở hữu; không gọi SaveAssets cho toàn project.
UniTask được cài qua UPM nếu chọn; SDK Ads/Firebase và thông tin tài khoản vẫn cần cấu hình trong Feature Manager.
Tắt công tắc không gỡ SDK hoặc loại binary SDK khỏi player.

Cần save/đóng scene chưa có tên nếu phải tạo scene mới, và save các chỉnh sửa trong thư mục sample trước khi chạy.
Wizard đăng ký Bootstrap/Home/InGame vào Editor build scene list. Build Profile ghi đè scene list vẫn cần thêm
ba scene tương ứng trong profile. Không tự thay đổi orientation hoặc áp dụng art Layer Lab; các menu cũ vẫn dùng được.

Báo cáo sau Setup: `Library/BaseUnity/LastSetupValidation.json`. **CompletedWithIssues** nghĩa là asset đã tạo xong
nhưng cấu hình còn lỗi, chẳng hạn thiếu ad IDs/Firebase config; dùng **Validate generated setup** hoặc Feature Manager.
Nếu compile lỗi, sửa Console/conflict rồi chọn **Retry pending setup**. Có thể clear pending status để giữ file/backup
và bắt đầu lại. Tác vụ bị ngắt khi đang tạo asset giữ các snapshot gốc; nếu chưa kịp hoàn tất journal,
có thể cần kiểm tra/khôi phục thủ công từ backup, không tự ghi đè trạng thái mới.

### Preview và xử lý conflict

Tab **Update preview** so sánh source trong submodule/package, file hiện tại và baseline lần cài trước.
Bản cài trước khi có Wizard có thể được đăng ký bằng **Apply selected operations / track identical files**:
file giống nguồn được theo dõi, file khác được giữ mặc định và hiện conflict; không suy đoán rằng đó là bản gốc.

| Trạng thái | Mặc định |
| --- | --- |
| Identical | Giữ file/GUID, ghi nhận baseline |
| Add / Update | Chọn thêm mới hoặc cập nhật file chưa chỉnh riêng |
| LocalModified | Giữ chỉnh sửa riêng khi nguồn chưa thay đổi |
| Conflict / LocalDeleted | Giữ nội dung hoặc quyết định xóa cục bộ; chọn checkbox chỉ khi muốn dùng nguồn |
| SourceRemoved | Giữ file cũ; chọn DELETE chỉ khi muốn xóa cả file và metadata |

**Diff** có dấu `+`/`-`, cùng tab Current/Source/Tracked baseline để xem toàn bộ nội dung. Không tự merge code.
Với file lớn/binary, xem các bản nội dung/thông tin được hiển thị; resolve bằng công cụ riêng nếu cần.
File thay đổi sau preview sẽ làm bước Apply dừng và yêu cầu scan lại. Kiểm tra cả file bị bỏ khỏi nguồn,
nhất là khi class đổi tên/vị trí, để tránh giữ hai bản class gây lỗi compile.

Công cụ chỉ quản lý file phân phối trong `Samples~/GameFlow`, bỏ qua `.meta`. Scene/config/prefab do generator tạo
không thuộc danh sách cập nhật code. GUID hiện có được giữ. Có thể chỉ Apply code mà không chạy bước Setup.
Nếu còn conflict chưa chọn dùng nguồn, installed version giữ phiên bản cũ/chưa fully tracked; baseline từng file vẫn được lưu.
Không cập nhật core submodule, UniTask sample hay SDK adapter bằng tab này; pull Git/import package tương ứng rồi review Game Flow.

### Backup và khôi phục

Manifest/baseline: `ProjectSettings/BaseUnity/Samples/*.json`, dùng khóa theo đường dẫn tương đối để di chuyển project
sang máy/thư mục khác vẫn giữ theo dõi. Backup/journal: `ProjectSettings/BaseUnity/SampleBackups/*.json`.
Lưu các dữ liệu theo dõi cùng project game nếu muốn chia sẻ workflow cho team; chúng không nằm trong Git của thư viện.

Mỗi lần Apply ghi backup trước khi thay file. Lỗi ghi sẽ thử rollback; nếu bị chặn do quyền/khóa file,
journal được giữ để phục hồi. Backup Setup chỉ giữ những file thực sự đổi/thêm và scene list nếu có thay đổi.
Chọn tab **Backups**, đọc danh sách file rồi **Validate and restore this backup**. Restore dừng trước khi ghi
nếu phát hiện chỉnh sửa mới hơn. Khôi phục mới nhất trước; bản cũ có thể bị chặn vì đã có thay đổi sau nó.
Rollback file mới sẽ xóa cả metadata được Unity tạo cho file đó; thư mục rỗng có thể còn lại.
Không force-overwrite các sửa đổi mới; cất/merge các chỉnh sửa đó trước hoặc phục hồi thủ công từ snapshot.
Backups chứa nội dung file của project, cần giữ nguyên journal để khôi phục.

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

## UniTask tùy chọn (0.10)

Object Pooling đã có trong core qua `PrefabPool`. Adapter UniTask mới nằm ở `Runtime/UniTask`, assembly
`BaseUnity.UniTask`, namespace `BaseUnity.Async`. Trong project BaseUnity đã cài **UniTask 2.5.11** qua UPM.

Với dự án khác dùng submodule, chọn **Tools > BaseUnity > Integrations > Install UniTask 2.5.11**, hoặc thêm
Git URL sau vào Package Manager > Install package from git URL:

```text
https://github.com/Cysharp/UniTask.git?path=src/UniTask/Assets/Plugins/UniTask#2.5.11
```

Theo [hướng dẫn UPM của Cysharp](https://github.com/Cysharp/UniTask#upm-package), dependency Git được khai báo
ở manifest của **dự án game**; thư viện không kèm source/binary UniTask. Adapter và test UniTask tự bật khi
UPM package `com.cysharp.unitask` phiên bản 2.5.0 trở lên tồn tại. Phiên bản đã kiểm thử là 2.5.11.
Core giữ API `Task` đang dùng; các API `...UniTaskAsync` chuyển đổi Task sang UniTask để giữ tương thích,
không phải viết lại toàn bộ core hoặc cam kết không cấp phát bộ nhớ.

Game có asmdef riêng thêm references: **BaseUnity.Runtime**, **BaseUnity.UniTask**, **UniTask**.
Mọi thao tác pool/scene/SDK adapter đều gọi từ Unity main thread.

```csharp
using System;
using BaseUnity.Async;
using BaseUnity.Pooling;
using Cysharp.Threading.Tasks;
using UnityEngine;

// Trong một MonoBehaviour đã tạo pool và sở hữu vòng đời các object:
async UniTask SpawnAsync(PrefabPool pool)
{
    var instance = pool.Rent(transform.position, transform.rotation);
    pool.TryGetRentalId(instance, out var rental);
    try
    {
        await pool.ReturnAfterAsync(instance, TimeSpan.FromSeconds(2),
            ignoreTimeScale: false, cancellationToken: destroyCancellationToken);
    }
    finally { pool.TryReturn(instance, rental); }
}
```

`ReturnAfterAsync` dùng `UniTask.Delay`: mặc định chịu `timeScale`, đặt `ignoreTimeScale: true` để tiếp tục khi pause.
Kết quả `true` khi timer trả object thành công, `false` nếu lượt thuê đã kết thúc hoặc pool bị dispose.
Rental ID đổi mỗi lần Rent, nên timer của lượt trước không trả nhầm object đã được thuê lại.
Caller hủy bằng token sẽ nhận `OperationCanceledException` và vẫn sở hữu object; `finally` trong ví dụ trả
đúng lượt thuê nếu còn tồn tại. Dispose pool hủy các timer và dọn cả object đang thuê.
Mỗi timer tạo linked cancellation source; đây không phải thao tác zero-allocation.

Adapter có sẵn:

- `SceneService.LoadUniTaskAsync` / `UnloadUniTaskAsync`.
- `AdsService.InitializeUniTaskAsync` / `LoadUniTaskAsync` / `ShowUniTaskAsync`.
- `FirebaseService.InitializeUniTaskAsync` / `FetchRemoteConfigUniTaskAsync`.
- `FeatureServices.InitializeUniTaskAsync`.

Cancellation của scene giữ nguyên: native load đã bắt đầu phải hoàn tất rồi mới báo hủy, giữ cờ busy trong thời gian đó.
Ads chỉ thưởng khi kết quả là `AdResult.Rewarded`; đóng/hủy hoặc tắt dịch vụ không được xem là nhận thưởng.
Wrapper không thêm cơ chế hủy sớm cho quảng cáo đang hiển thị.
Chỉ await một UniTask một lần; dùng API `.Preserve()`/`.AsTask()` của UniTask khi thực sự cần nhiều consumer.

Có sample **UniTask Pooling** trong Package Manager; dùng submodule thì copy `Samples~/UniTask` vào Assets.
Gắn `UniTaskPoolExample`, gán prefab rồi gọi `Spawn` từ gameplay hoặc UI button. Sample dispose pool khi owner bị hủy.
`PrefabPool` cũng có `CountAll`, `CountActive`, `CountInactive`, `TryGetRentalId`, `TryReturn` và `DisposalToken`.
Các hàm Rent/Return cũ vẫn sử dụng được như trước.

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

V0.9: **69/69 EditMode tests pass**, bổ sung kiểm tra scene list theo build, config không dùng,
Release/Development, warning policy, report và bảo toàn scene đang mở. Không thay đổi runtime;
bộ PlayMode 19 bài đã đạt ở v0.8. Chưa chạy xuất APK cho bản 0.9.

V0.10: **69/69 EditMode + 28/28 PlayMode tests pass** với UniTask 2.5.11 trên Unity 6000.3.26f1.
Assembly `BaseUnity.UniTask.Tests` chạy ở PlayMode, kiểm tra timer theo timeScale, cancellation, pool disposal,
callback Ads và lỗi scene qua adapter. Sample UniTask Pooling đã import và compile trong workspace.
Chưa kiểm thử tích hợp UniTask trong Android/iOS player; adapter tùy chọn khi không có package được cấu hình
bằng asmdef define constraints, chưa thực hiện lượt compile gỡ package trong lần cập nhật này.

V0.11: **85/85 EditMode + 28/28 PlayMode tests pass**. Đã dùng updater trên bản sample của BaseUnity,
giữ file không xác định là bản gốc, tiếp tục Setup qua compile/reload, tạo mới 3 scene + 6 prefab UI trong
thư mục kiểm tra rồi rollback và khôi phục scene list. Kiểm tra scene/UI của lần tạo mới không có lỗi.
Kiểm thử trên Windows/Unity 6000.3.26f1; chưa kiểm thử UPM import trên project/máy mới riêng biệt.

## Google Consent / UMP (0.12)

Mở **Tools > BaseUnity > Feature Manager > Ads**, bật Ads và **Enable Google consent**.
Thông tin consent chỉ hiện khi bật tính năng. Consent mặc định bật trong FeatureConfig;
Ads đang bật mà thiếu consent provider sẽ bị chặn an toàn, game vẫn tiếp tục chạy.

1. Import [Google Mobile Ads Unity plugin](https://developers.google.com/admob/unity/quick-start) (đã kiểm tra SDK 11.6.0).
2. Bấm **Install adapter** trong phần Google consent, rồi **Create and assign provider asset**.
3. Điền **AdMob App ID** Android/iOS (có dấu `~`) và bấm **Apply app IDs / SDK key to installed SDK**.
   ID này cần cả khi dùng MAX mediation; Apply cập nhật cả Google và MAX settings khi cả hai có mặt.
4. Tạo và **publish** privacy message cho đúng app trong AdMob > Privacy & messaging, khai báo đủ ad partners.
5. Nếu dùng MAX, tắt **Enable MAX Terms and Privacy Policy Flow** trong AppLovin Integration Manager
   để dùng một luồng UMP bên ngoài trước MAX. MAX đọc TCF/Additional Consent; không gán `SetHasUserConsent(true)` từ `CanRequestAds`.
6. Chạy Validate Build Configuration và kiểm tra form trên máy Android/iOS thật.

UMP cập nhật một lần mỗi phiên chạy, hiển thị form khi SDK yêu cầu. Chỉ khi `CanRequestAds()`
cho phép mới tạo Ads backend. Không có thời hạn tự đóng form khi người chơi đang đọc;
Home/gameplay vẫn hoạt động. Lỗi mạng chỉ cho phép Ads nếu chính UMP cho phép dùng quyết định từ phiên trước.
Khi đổi lựa chọn, mọi ad đã cache bị hủy, reward đang chờ bị chặn; lần tải tiếp theo tạo ad mới.
Nếu chưa có quyền, game không bị khóa và không tự cấp reward.

Settings của UI mẫu có **PRIVACY OPTIONS** khi UMP yêu cầu điểm mở lại; nếu khởi tạo lỗi,
hiện **RETRY PRIVACY**. Dự án đã cài sample cần review/update `GameFlowUI.cs`, `GameFlowView.cs`
và thêm `GameFlowPrivacyButton.cs` bằng Setup and Update Wizard; giữ nguyên prefab/art hiện có.
Có thể gắn `GameFlowPrivacyButton` vào button riêng qua `Initialize(consentService, errorText)`.

API cho game khác:
```csharp
var consent = bootstrap.Services.Get<BaseUnity.Features.ConsentService>();
if (consent.PrivacyOptionsRequired)
    await consent.ShowPrivacyOptionsAsync();
// Nút thử lại sau lỗi mạng:
await consent.RetryAsync();
```

`Consent testing` giả lập EEA/ngoài EEA bằng **UMP hashed device IDs** trong development build;
đây không phải MAX advertising IDs. Release player bỏ qua debug settings. `ResetForTesting()` chỉ chạy
trong Editor/development build và không tự gọi lúc mở app. Editor không giả lập việc đồng ý hoặc cấp thưởng.
TFUA được chuyển sang request của adapter AdMob mới; MAX bị chặn khi chọn đối tượng dưới tuổi consent.
Cờ này không thay thế hệ thống xác định tuổi/COPPA của game.

Đây là consent dành cho Ads; không tự đổi lựa chọn thu thập Firebase Analytics/Crashlytics,
không thay thế ATT trên iOS. SDK, App ID, provider asset và cấu hình tài khoản của project nằm ngoài Git thư viện.
Theo [Google UMP](https://developers.google.com/admob/unity/privacy) và
[MAX external CMP flow](https://support.applovin.com/en/max/unity/overview/terms-and-privacy-policy-flow).

Kiểm tra v0.12: **98/98 EditMode, 31/31 PlayMode đạt**, Unity Console không có lỗi.
Có mock cho consent/reward và kiểm tra button ở cả UI dựng bằng code lẫn prefab; chưa xác minh
form/native Ads trên thiết bị vì project còn thiếu AdMob App ID và message tài khoản đã publish.

## Mobile modules (0.13)

Cấu hình tại **Feature Manager > Modules** hoặc chọn module trong Setup Wizard:

| Module | Chức năng |
| --- | --- |
| Assets / Addressables | Lease load/release, cancellation, giải phóng handle trả về muộn, prefab theo màn |
| Tutorial | Lưu từng bước, highlight UI, chặn ngoài mục tiêu, đúng action mới tiến bước, Skip/Reset |
| Popup | Một popup đang mở, ưu tiên/FIFO, chống trùng ID, giới hạn số lượng, scope/cancellation |
| Feedback | Toast, loading nhiều chủ sở hữu và hộp xác nhận dùng chung |
| Haptics | Android/iOS, nhiều kiểu rung, giới hạn tần suất, lưu tùy chọn |
| Local notifications | Xin quyền chủ động, hẹn giờ UTC, Daily Reward, chỉ hủy ID do base sở hữu |
| IAP | Consumable/non-consumable, giá từ store, restore, retry pending, chống cấp trùng và Remove Ads |

Popup/Feedback mặc định bật; module SDK cần bật và gán provider. Tắt module sẽ ẩn thông tin của nó.
Core không phụ thuộc SDK. Cài package bằng **Tools > BaseUnity > Integrations > Install Mobile Module
Packages**, sau đó Install adapter / Create provider ở từng mục. Đã kiểm tra biên dịch với Addressables
4.1.1, Mobile Notifications 2.5.0, Unity IAP 5.4.4. SDK/asset cấu hình chỉ nằm ở host project.

Game Flow có nút **MORE** cho rung/thông báo/tutorial/store; giữ prefab và art hiện có.
`LevelData.addressablePrefab` ưu tiên hơn prefab trực tiếp, giữ lease đến khi rời màn.
Tutorial có key `home.play`, `gameplay.collect`; target riêng dùng TutorialTarget. Popup xác nhận tạm
pause gameplay, chờ Ads/Consent hoặc menu Offline để tránh chồng UI.

IAP chỉ cấp sau xác minh receipt và lưu ví + mã giao dịch thành công, rồi mới confirm với store.
Restore chờ xác minh/ghi entitlement xong mới báo hoàn tất. Remove Ads tắt banner/interstitial, giữ
rewarded tùy chọn. Cần sản phẩm/store IDs trên Play Console/App Store Connect và endpoint HTTPS tin cậy
của bạn: base cung cấp client adapter/protocol, không có backend được host sẵn. Editor không cấp từ giao
dịch giả lập. Không hỗ trợ subscription hoặc giỏ nhiều sản phẩm/số lượng lớn hơn 1.

Offline save nâng thêm trường lên schema 3 khi ghi thành công, giữ tiền/tiến trình cũ; bản base cũ chặn
schema mới. Rung, thông báo và giao dịch sandbox vẫn cần thử trên thiết bị Android/iOS.
Xem [hướng dẫn đầy đủ và API](Samples~/MobileModules/README.md).

Kiểm tra v0.13: **127/127 EditMode của BaseUnity và 35/35 PlayMode đạt**, gồm retry receipt, lỗi lưu,
chống cấp trùng, giữ/release asset theo màn, Tutorial thao tác nút và popup pause/resume. Đã gán provider
mới ở project BaseUnity; IAP đang tắt, chờ store IDs và endpoint xác minh. Chưa chạy giao dịch sandbox,
thông báo/rung trên thiết bị thật. Switch runtime không tự loại SDK đã cài khỏi bản build.

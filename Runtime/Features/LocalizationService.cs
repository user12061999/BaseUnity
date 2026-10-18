using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BaseUnity.Save;

namespace BaseUnity.Features
{
    public sealed class LocalizationService
    {
        [Serializable] public sealed class LanguageSave { public string language; }
        private readonly ISaveStore saves;
        private readonly Dictionary<string, string> catalog = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly List<Tuple<Regex, string>> patterns = new List<Tuple<Regex, string>>();
        public bool Enabled { get; }
        public string Language { get; private set; }
        public string LastError { get; private set; }
        public event Action Changed;
        public LocalizationService(LocalizationSettings settings, ISaveStore saves)
        {
            settings = settings ?? new LocalizationSettings(); this.saves = saves; Enabled = settings.enabled;
            Language = settings.defaultLanguage == "vi" ? "vi" : "en";
            if (Enabled && saves.TryLoad<LanguageSave>("baseunity_language", out var saved) && (saved != null && (saved.language == "vi" || saved.language == "en"))) Language = saved.language;
            string[] pairs = {
                "MORE|THÊM", "SETTINGS & STORE|CÀI ĐẶT & CỬA HÀNG", "HAPTICS ON|BẬT RUNG", "HAPTICS OFF|TẮT RUNG", "NOTIFICATIONS ON|BẬT THÔNG BÁO", "ENABLE NOTIFICATIONS|CHO PHÉP THÔNG BÁO", "TUTORIAL|HƯỚNG DẪN", "SKIP|BỎ QUA", "NEXT|TIẾP", "OK|ĐỒNG Ý", "RESTORE PURCHASES|KHÔI PHỤC GIAO DỊCH", "RETRY PURCHASE DELIVERY|THỬ NHẬN GÓI ĐÃ MUA", "Store unavailable|Cửa hàng chưa sẵn sàng", "Purchase unavailable|Chưa thể mua hàng", "Purchase pending|Đang chờ xác minh giao dịch", "Restore unavailable|Chưa thể khôi phục giao dịch", "Restore completed|Đã khôi phục giao dịch", "Restoring purchases...|Đang khôi phục giao dịch...", "Notifications unavailable|Chưa thể bật thông báo", "Disable notifications?|Tắt thông báo?", "Daily reminders will be cancelled.|Các lời nhắc hằng ngày sẽ được hủy.", "Could not save. Try again.|Không lưu được. Hãy thử lại.",
                "PRIVACY OPTIONS|QUYỀN RIÊNG TƯ", "Privacy options|Quyền riêng tư", "RETRY PRIVACY|THỬ LẠI QUYỀN RIÊNG TƯ", "Retry privacy|Thử lại quyền riêng tư",
                "Privacy options are unavailable.|Chưa thể mở tùy chọn quyền riêng tư.", "Privacy options are unavailable. Please try again.|Chưa thể mở tùy chọn quyền riêng tư. Hãy thử lại.",
                "Saving...|Đang lưu...", "Restored result|Kết quả đã khôi phục",
                "MENU|MENU", "Reward save failed. Try again.|Không lưu được dữ liệu. Hãy thử lại.", "PERSONAL BEST|KỶ LỤC CÁ NHÂN", "A NEW ADVENTURE AWAITS|CHUYẾN PHIÊU LƯU MỚI ĐANG CHỜ", "READY TO PLAY?|SẴN SÀNG CHƠI?", "Jump in. Collect points.|Vào chơi. Thu thập điểm.", "Beat your personal best.|Vượt qua kỷ lục của bạn.", "Time is up. Try again!|Hết giờ. Hãy thử lại!", "RETURN HOME|VỀ TRANG CHỦ",

                "Play|Chơi", "Settings|Cài đặt", "Choose Level|Chọn màn", "Pause|Tạm dừng", "Resume|Tiếp tục", "Retry|Chơi lại", "Next Level|Màn tiếp theo", "Home|Trang chủ", "Return Home|Về trang chủ", "Cancel|Hủy", "Save & Back|Lưu và trở về",
                "PLAY|CHƠI", "SETTINGS|CÀI ĐẶT", "CHOOSE LEVEL|CHỌN MÀN", "PAUSE|TẠM DỪNG", "PAUSED|TẠM DỪNG", "RESUME|TIẾP TỤC", "RETRY|CHƠI LẠI", "NEXT LEVEL|MÀN TIẾP THEO", "HOME|TRANG CHỦ", "BACK HOME|VỀ TRANG CHỦ", "CANCEL|HỦY", "SAVE|LƯU",
                "SHOP|CỬA HÀNG", "DAILY REWARD|QUÀ HẰNG NGÀY", "MISSIONS|NHIỆM VỤ", "INVENTORY|TÚI ĐỒ", "CLAIM|NHẬN", "CLAIMED|ĐÃ NHẬN", "BUY|MUA", "CLOSE|ĐÓNG", "Coin|Xu", "Gem|Ngọc", "Energy|Năng lượng", "Ticket|Vé",
                "LEVEL COMPLETE!|HOÀN THÀNH MÀN!", "ALL LEVELS COMPLETE!|HOÀN THÀNH TẤT CẢ!", "LEVEL FAILED|THẤT BẠI", "Level completed!|Đã hoàn thành màn!", "You can do it. Try again!|Hãy thử lại nhé!", "Time is up!|Hết giờ!", "NO TIME LIMIT|KHÔNG GIỚI HẠN GIỜ", "LOCKED|CHƯA MỞ", "COMPLETED|HOÀN THÀNH", "SCORE|ĐIỂM", "BEST SCORE|ĐIỂM CAO NHẤT", "SESSIONS|LƯỢT CHƠI",
                "SOUND OFF|TẮT TIẾNG", "SOUND ON|BẬT TIẾNG", "Sound: Off|Âm thanh: Tắt", "Sound: On|Âm thanh: Bật", "Master|Tổng", "Music|Nhạc", "Effects|Hiệu ứng", "MASTER|TỔNG", "MUSIC|NHẠC", "EFFECTS|HIỆU ỨNG", "Volume +|Tăng âm lượng", "Volume -|Giảm âm lượng",
                "Getting ready|Đang chuẩn bị", "Loading...|Đang tải...", "Unable to continue|Không thể tiếp tục", "Not enough currency|Không đủ tiền", "Not enough items|Không đủ vật phẩm", "Purchase limit reached|Đã đạt giới hạn mua", "Daily reward already claimed|Đã nhận quà hôm nay", "Mission is not ready|Nhiệm vụ chưa hoàn thành", "Reward already claimed or expired|Phần thưởng đã nhận hoặc hết hạn", "Saved|Đã lưu", "Try again|Thử lại", "Ad unavailable|Quảng cáo chưa sẵn sàng", "Waiting for ad...|Đang chờ quảng cáo...", "X2 REWARD|NHÂN ĐÔI THƯỞNG", "RETRY REWARD SAVE|THỬ LƯU THƯỞNG LẠI", "No items|Chưa có vật phẩm",
                "Play 3 games|Chơi 3 lượt", "Win 2 games|Thắng 2 lượt", "Score 100 points|Đạt 100 điểm", "First steps|Khởi đầu", "Keep going|Tiếp bước", "Final challenge|Thử thách cuối", "YOUR GAME|GAME CỦA BẠN",
                "PLAY LEVEL {0}|CHƠI MÀN {0}", "LEVEL {0}|MÀN {0}", "TARGET  {0}|MỤC TIÊU  {0}", "TARGET {0}|MỤC TIÊU {0}", "TIME  {0}s|THỜI GIAN  {0}giây", "LOADING  {0}%|ĐANG TẢI  {0}%", "Loading  {0}%|Đang tải  {0}%", "DAY {0}|NGÀY {0}", "REWARD +{0} Coin|THƯỞNG +{0} Xu", "Master volume  {0}%|Âm lượng tổng  {0}%",
                "BEST {0}    /    SESSIONS {1}|CAO NHẤT {0}    /    LƯỢT CHƠI {1}", "LEVEL {0}  /  SCORE {1}|MÀN {0}  /  ĐIỂM {1}", "SCORE  {0}|ĐIỂM  {0}", "TARGET {0}  /  TIME {1}s|MỤC TIÊU {0}  /  THỜI GIAN {1}giây", "{0} / {1} COMPLETED|{0} / {1} ĐÃ HOÀN THÀNH",
                "A starting point for your next game|Nền tảng cho game của bạn", "Collect +10|Nhận +10", "Finish|Hoàn thành", "Win|Thắng", "Lose|Thua", "All levels complete!|Hoàn thành tất cả các màn!", "LEVEL COMPLETED|HOÀN THÀNH MÀN"
            };
            foreach (string pair in pairs) { var parts = pair.Split('|'); catalog[parts[0]] = parts[1]; }
            foreach (var item in settings.translations ?? Array.Empty<Translation>())
                if (item != null && !string.IsNullOrEmpty(item.english) && !string.IsNullOrEmpty(item.vietnamese)) catalog[item.english] = item.vietnamese;
            foreach (var entry in catalog.Where(x => x.Key.Contains("{0}" )).OrderByDescending(x => x.Key.Length))
            {
                string expression = Regex.Escape(entry.Key);
                for (int i = 0; i < 8; i++) expression = expression.Replace(Regex.Escape("{" + i + "}"), "(?<p" + i + ">.+?)");
                patterns.Add(Tuple.Create(new Regex("^" + expression + "$", RegexOptions.CultureInvariant), entry.Value));
            }
        }
        public bool SetLanguage(string language)
        {
            if (!Enabled || (language != "en" && language != "vi")) return false;
            try { saves.Save("baseunity_language", new LanguageSave { language = language }); }
            catch (Exception e) { LastError = e.Message; return false; }
            Language = language; LastError = null;
            if (Changed != null) foreach (Action listener in Changed.GetInvocationList())
                try { listener(); } catch (Exception e) { UnityEngine.Debug.LogException(e); }
            return true;
        }
        public string Text(string source)
        {
            if (string.IsNullOrEmpty(source) || !Enabled || Language != "vi") return source;
            if (catalog.TryGetValue(source, out var translated)) return translated;
            if (source.Contains("\n")) return string.Join("\n", source.Split('\n').Select(Text));
            foreach (var pair in patterns)
            {
                var match = pair.Item1.Match(source); if (!match.Success) continue;
                string result = pair.Item2;
                for (int i = 0; i < 8; i++) if (match.Groups["p" + i].Success) result = result.Replace("{" + i + "}", match.Groups["p" + i].Value);
                return result;
            }
            return source;
        }
    }
}

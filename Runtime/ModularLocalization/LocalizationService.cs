using System;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using BaseUnity.Save;
namespace BaseUnity.Localization
{
    public sealed class LocalizationService : IDisposable
    {
        [Serializable] public sealed class LanguageState { public string language; }
        private readonly ISaveStore saves;
        private readonly Dictionary<string,LocalizedEntry> entries=new Dictionary<string,LocalizedEntry>();
        private readonly List<Tuple<Regex,LocalizedEntry>> patterns=new List<Tuple<Regex,LocalizedEntry>>();
        public string Language { get; private set; }
        public CultureInfo Culture => CultureInfo.GetCultureInfo(Language=="vi"?"vi-VN":"en-US");
        public event Action Changed;
        public string LastError { get; private set; }
        public LocalizationService(ISaveStore saves,LocalizedEntry[] custom=null,string defaultLanguage="en")
        {
            if(defaultLanguage!="en"&&defaultLanguage!="vi")throw new ArgumentException("Supported languages: en, vi");
            this.saves=saves??throw new ArgumentNullException(nameof(saves));Language=defaultLanguage;
            LanguageState saved;var json=saves as JsonSaveStore;
            if(json!=null) { json.LoadVersioned("baseunity_modular_language",out saved,1,s=>s!=null&&(s.language=="en"||s.language=="vi")); }
            else saves.TryLoad("baseunity_modular_language",out saved);
            if(saved!=null&&(saved.language=="en"||saved.language=="vi"))Language=saved.language;
            foreach(var e in Builtins())entries[e.key]=e;
            var keys=new HashSet<string>();
            foreach(var e in custom??new LocalizedEntry[0]) {
                if(e==null||string.IsNullOrWhiteSpace(e.key)||!keys.Add(e.key)||string.IsNullOrEmpty(e.english)||string.IsNullOrEmpty(e.vietnamese))throw new ArgumentException("Duplicate/empty localization entry");
                entries[e.key]=new LocalizedEntry { key=e.key,english=e.english,vietnamese=e.vietnamese };
            }
            foreach(var e in entries.Values.OrderByDescending(e=>e.english.Length))if(e.english.Contains("{0}")) {
                string pattern=Regex.Escape(e.english);for(int i=0;i<8;i++)pattern=pattern.Replace(Regex.Escape("{"+i+"}"),"(?<p"+i+">.+?)");
                patterns.Add(Tuple.Create(new Regex("^"+pattern+"$",RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100)),e));
            }
        }
        public bool SetLanguage(string language)
        {
            if(language!="en"&&language!="vi")return false;
            try { saves.Save("baseunity_modular_language",new LanguageState { language=language }); }
            catch(Exception error) { LastError=error.Message;return false; }
            Language=language;LastError=null;
            if(Changed!=null)foreach(Action observer in Changed.GetInvocationList())try { observer(); }catch(Exception error) { LastError=error.Message; }
            return true;
        }
        public string Format(string key,params object[] args)
        {
            var text=entries.TryGetValue(key,out var e)?(Language=="vi"?e.vietnamese:e.english):key;
            try { return string.Format(Culture,text,args); }catch(FormatException) { return text; }
        }
        public string Text(string source)
        {
            if(string.IsNullOrEmpty(source))return source;
            if(entries.TryGetValue(source,out var direct))return Language=="vi"?direct.vietnamese:direct.english;
            var exact=entries.Values.FirstOrDefault(e=>e.english==source);if(exact!=null)return Language=="vi"?exact.vietnamese:exact.english;
            if(Language!="vi")return source;
            foreach(var pair in patterns) {
                var match=pair.Item1.Match(source);if(!match.Success)continue;
                var args=new object[8];for(int i=0;i<8;i++) { string value=match.Groups["p"+i].Value;args[i]=decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var number)?(object)number:value; }
                try { return string.Format(Culture,pair.Item2.vietnamese,args); }catch(FormatException) { return source; }
            }
            return source;
        }
        public string Number(decimal value,string format="N0") => value.ToString(format,Culture);
        public void Dispose() { Changed=null; }
        private static IEnumerable<LocalizedEntry> Builtins()
        {
            string[] pairs={"Play|Chơi","Home|Trang chủ","Ingame|Trong game","Loading|Đang tải","Pause|Tạm dừng","Resume|Tiếp tục","Win|Chiến thắng","Lose|Thất bại","Retry|Chơi lại","Next Level|Màn tiếp theo","Demo Win|Thử thắng","Demo Lose|Thử thua","Demo starter (once)|Quà thử (một lần)","Demo win +1|Thử thắng +1","Shop|Cửa hàng","SHOP|CỬA HÀNG","Daily Reward|Quà hằng ngày","DAILY REWARD|QUÀ HẰNG NGÀY","Quests|Nhiệm vụ","QUEST|NHIỆM VỤ","Achievements|Thành tựu","ACHIEVEMENT|THÀNH TỰU","ECONOMY SAMPLE|MẪU KINH TẾ","Claim|Nhận","Claimed|Đã nhận","Buy|Mua","Close|Đóng","Success|Thành công","Already received|Đã nhận rồi","Not available|Chưa thể thực hiện","Could not save. Please retry.|Không lưu được. Hãy thử lại.","Ready|Sẵn sàng","Red skin|Trang phục đỏ","10 Gems|10 Ngọc","Win 3 games (daily UTC)|Thắng 3 lượt (ngày UTC)","Win 3 games total|Thắng tổng cộng 3 lượt","coin: {0}|Xu: {0:N0}","gem: {0}|Ngọc: {0:N0}","{0} coin|{0:N0} Xu","{0} gem|{0:N0} Ngọc","Purchased: {0} / {1}|Đã mua: {0:N0} / {1:N0}","Purchased: {0}|Đã mua: {0:N0}","Day {0} / {1}|Ngày {0} / {1}","Level {0} / {1}|Màn {0} / {1}","Wait {0}|Chờ {0}","Win 3 games|Thắng 3 lượt","Saving rewards... retrying|Đang lưu thưởng... thử lại","MOBILE DEMO|GAME MẪU MOBILE","Tap|Chạm","Energy {0} / {1}|Năng lượng {0} / {1}","Energy full|Năng lượng đầy","Next energy in {0}s|Hồi sau {0} giây","Player level {0}|Cấp người chơi {0}","EXP {0} / {1}|EXP {0} / {1}","MAX LEVEL|CẤP TỐI ĐA","Refill {0} coin|Đổi {0:N0} Xu","Not enough energy|Không đủ năng lượng","Energy received|Đã nhận năng lượng","Targets: {0} / {1}|Mục tiêu: {0} / {1}","Time: {0}s|Thời gian: {0} giây","Coins gained: {0}  EXP: {1}|Nhận {0:N0} Xu  EXP: {1:N0}","LEVEL COMPLETE|HOÀN THÀNH MÀN"};
            foreach(string p in pairs) { var v=p.Split('|');yield return new LocalizedEntry { key=v[0],english=v[0],vietnamese=v[1] }; }
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BaseUnity.GameFlow
{
    /// <summary>Paginated level selection, shared by the plain and Layer Lab views.</summary>
    public sealed class LevelSelectionView : MonoBehaviour
    {
        private GameFlowBootstrap flow;
        private Transform content;
        private Font font;
        private Sprite buttonSprite;
        private int page;
        private const int PageSize = 6;

        public static LevelSelectionView Create(GameFlowBootstrap flow, Transform parent, Font font, Sprite buttonSprite = null)
        {
            var root = new GameObject("Level Selection", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.transform.SetParent(parent, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
            root.GetComponent<Image>().color = new Color(0.025f, 0.08f, 0.18f, 0.99f);
            var view = root.AddComponent<LevelSelectionView>();
            view.flow = flow; view.font = font; view.buttonSprite = buttonSprite;
            flow.Changed += view.Refresh;
            view.Refresh();
            return view;
        }
        private void Refresh()
        {
            bool visible = flow.Phase == FlowPhase.LevelSelect;
            if (visible && !gameObject.activeSelf) page = (flow.HomeLevel - 1) / PageSize;
            gameObject.SetActive(visible);
            if (visible) Draw();
        }
        private void Draw()
        {
            if (content) { content.gameObject.SetActive(false); Destroy(content.gameObject); }
            content = MakeRect("Level grid", transform, 900, 1600, 0, 0);
            int pages = Mathf.Max(1, Mathf.CeilToInt(flow.Config.LevelCount / (float)PageSize));
            page = Mathf.Clamp(page, 0, pages - 1);
            Label(content, "CHOOSE LEVEL", 0, 620, 54);
            Label(content, flow.Progress.highestCompletedLevel + " / " + flow.Config.LevelCount + " COMPLETED", 0, 530, 28);
            Button firstUnlocked = null;
            for (int i = 0; i < PageSize; i++)
            {
                int number = page * PageSize + i + 1;
                if (number > flow.Config.LevelCount) break;
                bool unlocked = flow.IsLevelUnlocked(number);
                var definition = flow.Config.GetLevel(number);
                string state = !unlocked ? "LOCKED" : flow.IsLevelCompleted(number) ? "COMPLETED" : "PLAY";
                var button = MakeButton("Level " + number, content,
                    number + "  " + (definition?.title ?? "Level") + "\n" + state,
                    (i % 2 == 0 ? -200 : 200), 330 - (i / 2) * 250, 360, 190);
                button.interactable = unlocked;
                button.onClick.AddListener(() => flow.StartLevel(number));
                if (unlocked && !firstUnlocked) firstUnlocked = button;
            }
            var previous = MakeButton("Previous page", content, "<", -260, -470, 140, 90);
            previous.interactable = page > 0; previous.onClick.AddListener(() => { page--; Draw(); });
            Label(content, (page + 1) + " / " + pages, 0, -470, 30);
            var next = MakeButton("Next page", content, ">", 260, -470, 140, 90);
            next.interactable = page + 1 < pages; next.onClick.AddListener(() => { page++; Draw(); });
            var back = MakeButton("Back to Home", content, "BACK HOME", 0, -640, 640, 110);
            back.onClick.AddListener(flow.CloseLevels);
            if (EventSystem.current) EventSystem.current.SetSelectedGameObject(firstUnlocked ? firstUnlocked.gameObject : back.gameObject);
        }
        private static RectTransform MakeRect(string name, Transform parent, float w, float h, float x, float y)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false); rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(w, h); rect.anchoredPosition = new Vector2(x, y); return rect;
        }
        private Text Label(Transform parent, string caption, float x, float y, int size)
        {
            var text = MakeRect("Caption", parent, 800, 80, x, y).gameObject.AddComponent<Text>();
            text.font = font; text.fontSize = size; text.text = caption; text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white; text.raycastTarget = false; return text;
        }
        private Button MakeButton(string name, Transform parent, string caption, float x, float y, float w, float h)
        {
            var rect = MakeRect(name, parent, w, h, x, y);
            var image = rect.gameObject.AddComponent<Image>(); image.sprite = buttonSprite; image.type = Image.Type.Sliced;
            image.color = buttonSprite ? Color.white : new Color(0.12f, 0.4f, 0.63f);
            var button = rect.gameObject.AddComponent<Button>(); button.targetGraphic = image;
            var text = Label(rect, caption, 0, 0, 30); text.rectTransform.sizeDelta = new Vector2(w - 30, h - 20);
            text.resizeTextForBestFit = true; text.resizeTextMinSize = 18; text.resizeTextMaxSize = 30;
            return button;
        }
        private void OnDestroy() { if (flow) flow.Changed -= Refresh; }
    }
}

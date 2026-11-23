using UnityEngine;
using UnityEngine.UI;

namespace LabSorting.Sorting
{
    [DisallowMultipleComponent]
    public sealed class RatRackSign : MonoBehaviour
    {
        [Min(.1f)] public float topOffset = .55f;
        public float fadeStart = 9f;
        public float hideDistance = 12f;
        public Camera view;
        [SerializeField] Transform presentation, card;
        [SerializeField] Text countText;
        [SerializeField] CanvasGroup visibility;
        SortRack rack;
        Renderer enclosure;
        int previousCount = -1, previousCapacity = -1;

        void Awake()
        {
            rack = GetComponent<SortRack>();
            enclosure = GetComponent<Renderer>();
            if (!presentation) Build();
            HideOldLabel();
        }

        void HideOldLabel()
        {
            if (rack.label && rack.label.TryGetComponent<Renderer>(out var renderer)) renderer.enabled = false;
        }

        // Called by the editor setup as well as when a rack has no saved signage.
        public void Build()
        {
            rack = GetComponent<SortRack>();
            enclosure = GetComponent<Renderer>();
            if (!rack || !rack.ratsOnly || !enclosure || presentation) return;
            presentation = new GameObject("Rat Rack Signage").transform;
            presentation.SetParent(transform, false);
            var scale = transform.lossyScale;
            presentation.localScale = new Vector3(1 / scale.x, 1 / scale.y, 1 / scale.z);
            var bounds = enclosure.bounds;
            card = Panel("Floating Count", bounds.center + Vector3.up * (bounds.extents.y + topOffset), Quaternion.identity, new Vector2(360, 142), .0025f);
            visibility = card.gameObject.AddComponent<CanvasGroup>();
            Icon(card, new Vector2(-125, 0), new Vector2(86, 78));
            Label(card, "RATS", new Vector2(38, 37), new Vector2(220, 40), 25, new Color(.55f, .91f, .84f));
            countText = Label(card, "0 / " + rack.Capacity, new Vector2(38, -14), new Vector2(220, 70), 52, Color.white);
            // Opposite fixed plates keep the enclosure identifiable from both aisles.
            for (int side = -1; side <= 1; side += 2)
            {
                var plate = Panel("Fixed Rat Nameplate", new Vector3(bounds.center.x + side * (bounds.extents.x + .012f), bounds.center.y, bounds.center.z), Quaternion.Euler(0, side < 0 ? 90 : -90, 0), new Vector2(320, 94), .0022f);
                Icon(plate, new Vector2(-108, 0), new Vector2(76, 66));
                Label(plate, "RATS", new Vector2(37, 0), new Vector2(205, 76), 44, Color.white);
            }
            HideOldLabel();
            RefreshCount();
        }

        Transform Panel(string title, Vector3 position, Quaternion rotation, Vector2 size, float scale)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Canvas));
            var rect = (RectTransform)go.transform;
            rect.SetParent(presentation, false);
            rect.SetPositionAndRotation(position, rotation);
            rect.localScale = Vector3.one * scale;
            rect.sizeDelta = size;
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var border = Element<Image>(rect, "Teal border", Vector2.zero, size);
            border.color = new Color(.22f, .69f, .61f);
            var background = Element<Image>(rect, "Opaque background", Vector2.zero, size - Vector2.one * 6);
            background.color = new Color(.035f, .10f, .12f);
            return rect;
        }

        static T Element<T>(Transform parent, string title, Vector2 position, Vector2 size) where T : Graphic
        {
            var go = new GameObject(title, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var graphic = go.AddComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        static void Icon(Transform parent, Vector2 position, Vector2 size)
        {
            Element<RatSilhouetteGraphic>(parent, "Rat silhouette", position, size).color = new Color(.78f, .96f, .90f);
        }

        static Text Label(Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var label = Element<Text>(parent, text, position, size);
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.fontStyle = FontStyle.Bold;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = color;
            label.text = text;
            return label;
        }

        public void RefreshCount()
        {
            if (!rack) rack = GetComponent<SortRack>();
            if (!countText || (previousCount == rack.Count && previousCapacity == rack.Capacity)) return;
            previousCount = rack.Count;
            previousCapacity = rack.Capacity;
            countText.text = rack.Count + " / " + rack.Capacity;
            countText.color = rack.IsFull ? new Color(.55f, 1f, .76f) : Color.white;
        }

        public void FaceCamera(Camera camera)
        {
            if (!card || !camera || !enclosure) return;
            var bounds = enclosure.bounds;
            card.position = bounds.center + Vector3.up * (bounds.extents.y + topOffset);
            // World-space UI faces along local -Z. Full rotation also supports looking down/up.
            var direction = card.position - camera.transform.position;
            if (direction.sqrMagnitude > .0001f) card.rotation = Quaternion.LookRotation(direction, camera.transform.up);
            float distance = direction.magnitude;
            visibility.alpha = 1f - Mathf.InverseLerp(fadeStart, Mathf.Max(fadeStart + .1f, hideDistance), distance);
            foreach (Transform plate in presentation)
                if (plate != card && plate.TryGetComponent<Canvas>(out var canvas))
                    canvas.enabled = Vector3.Dot(plate.forward, camera.transform.position - plate.position) < 0;
        }

        void LateUpdate()
        {
            if (!view || !view.isActiveAndEnabled)
            {
                var session = FindFirstObjectByType<SortingSession>();
                view = session && session.player ? session.player.view : Camera.main;
            }
            RefreshCount();
            FaceCamera(view);
        }
    }
}

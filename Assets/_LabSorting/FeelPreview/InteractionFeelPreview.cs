using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using LabSorting.Sorting;

namespace LabSorting.FeelPreview
{
    // Isolated presentation sandbox. No SortingSession, persistence, economy or production overrides.
    public sealed class InteractionFeelPreview : MonoBehaviour
    {
        public Camera view;
        public SortRack rack;
        public SortItem[] templates;
        public AudioClip pickClip, placeClip, dropClip;
        public Material placementPreviewMaterial;
        public bool PreviewVisible { get; private set; }
        public bool proposed = true;
        public int effects = 1;
        public bool batch;
        public bool slow;
        public int model;
        public int CompletedActions { get; private set; }
        public int Arrivals { get; private set; }
        public int HeldCount => held.Count;
        public int BusyCount => busy.Count;
        public string Status { get; private set; }
        readonly List<SortItem> samples = new List<SortItem>();
        readonly List<SortItem> held = new List<SortItem>();
        readonly HashSet<SortItem> busy = new HashSet<SortItem>();
        readonly List<GameObject> transient = new List<GameObject>();
        Transform hand;
        AudioSource audioSource;
        UnityEngine.UI.Text statusText, modeText;
        UnityEngine.UI.Button modeButton, fxButton, batchButton, speedButton;
        SortItem hover;
        bool autoRunning;
        float cooldown;
        Material sparkMaterial;

        void Start()
        {
            Application.runInBackground = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            hand = new GameObject("Preview hand socket").transform;
            hand.SetParent(view.transform, false);
            hand.localPosition = new Vector3(.38f, -.09f, .9f);
            BuildUI();
            ResetPreview();
        }

        public void ResetPreview()
        {
            StopAllCoroutines();
            autoRunning = false;
            busy.Clear(); held.Clear(); hover = null;
            foreach (var item in samples)
                if (item) { item.CompleteMotion(); item.transform.DOKill(); Destroy(item.gameObject); }
            samples.Clear();
            foreach (var go in transient) if (go) Destroy(go);
            transient.Clear();
            rack.Initialize(); rack.Preview(null);
            for (int i = 0; i < 4; i++)
            {
                var item = Instantiate(templates[model], transform);
                item.name = "Preview sample " + (i + 1);
                item.gameObject.SetActive(true);
                item.location = ItemLocation.Loose; item.rack = null; item.slot = -1;
                item.originalScale = templates[model].transform.localScale;
                var bounds = SortRack.RotatedBounds(item.definition, Quaternion.identity);
                item.Snap(new Vector3(-1.05f + (i % 2) * .32f, .74f - bounds.min.y, -.55f + (i / 2) * .35f), Quaternion.identity);
                samples.Add(item);
            }
            Physics.SyncTransforms(); cooldown = 0;
            SetStatus("Click an item or PICK. Then PLACE / DROP. AUTO DEMO shows the sequence.");
            UpdateLabels();
        }

        public void SetMode(bool value) { proposed = value; ResetPreview(); }
        public void SetEffects(int value) { effects = Mathf.Clamp(value, 0, 2); UpdateLabels(); }
        public void NextModel() { model = (model + 1) % templates.Length; ResetPreview(); }
        void SetStatus(string value) { Status = value; if (statusText) statusText.text = value; }
        void Sound(AudioClip clip, float gain = .5f, float pitch = 1)
        { if (clip) { audioSource.pitch = pitch; audioSource.PlayOneShot(clip, gain); } }

        public void PickNext()
        {
            if (!proposed && Time.unscaledTime < cooldown) return;
            SortItem next = null;
            foreach (var item in samples) if (item.location == ItemLocation.Loose && !busy.Contains(item) && !item.Moving) { next = item; break; }
            if (next) Pick(next); else SetStatus("No loose items. RESET gives you four fresh samples.");
        }
        void Pick(SortItem item)
        {
            if (busy.Contains(item) || item.Moving || item.location == ItemLocation.Hand) return;
            if (!proposed && Time.unscaledTime < cooldown) return;
            if (item.rack) item.rack.Remove(item);
            if (held.Count > 0)
            {
                var previous = held[held.Count - 1];
                if (!busy.Contains(previous)) { previous.CompleteMotion(); previous.gameObject.SetActive(false); }
            }
            item.Highlight(false); item.location = ItemLocation.Hand;
            held.Add(item); busy.Add(item); cooldown = Time.unscaledTime + .24f;
            Sound(pickClip);
            if (proposed) StartCoroutine(PickMotion(item));
            else item.DockAt(hand, false, 0, () => FinishPick(item));
            SetStatus("Picked " + item.definition.displayName + "  |  In hand: " + held.Count);
        }
        void FinishPick(SortItem item)
        {
            busy.Remove(item); CompletedActions++;
            if (held.Count > 0 && held[held.Count - 1] != item) item.gameObject.SetActive(false);
        }
        IEnumerator PickMotion(SortItem item)
        {
            item.hitbox.enabled = false;
            Vector3 from = item.transform.position, scale = item.transform.localScale;
            Quaternion rotation = item.transform.rotation;
            Burst(from + Vector3.up * .08f, false);
            float t = 0;
            while (t < 1)
            {
                t = Mathf.Min(1, t + Time.deltaTime / .30f);
                float u = Mathf.Clamp01((t - .2f) / .8f), eased = u * u * (3 - 2 * u);
                Vector3 raised = from + Vector3.up * .12f;
                Vector3 middle = (raised + hand.position) * .5f + Vector3.up * .16f;
                item.transform.position = t < .2f ? Vector3.Lerp(from, raised, 1 - Mathf.Pow(1 - t / .2f, 2)) :
                    (1 - eased) * (1 - eased) * raised + 2 * (1 - eased) * eased * middle + eased * eased * hand.position;
                item.transform.rotation = Quaternion.Slerp(rotation, hand.rotation, eased) * Quaternion.Euler(new Vector3(8, 35, -12) * Mathf.Sin(t * Mathf.PI));
                item.transform.localScale = Vector3.Lerp(scale, item.originalScale * item.CarryScale, eased) * (1 + .045f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            item.Snap(hand.position, hand.rotation, hand);
            FinishPick(item);
        }

        public void Place()
        {
            if (held.Count == 0) { SetStatus("PICK an item first."); return; }
            if (busy.Count > 0) { SetStatus("Item is arriving. Try PLACE after it reaches your hand."); return; }
            if (!rack.CanAccept(held[held.Count - 1].definition)) { SetStatus("This rack cannot accept the held item."); return; }
            int count = proposed && batch ? held.Count : 1;
            for (int i = 0; i < count; i++)
            {
                var item = held[held.Count - 1];
                int slot = rack.Add(item);
                if (slot < 0) break;
                held.RemoveAt(held.Count - 1);
                item.gameObject.SetActive(true); item.transform.SetParent(null, true);
                item.Snap(hand.position, hand.rotation);
                item.transform.localScale = item.originalScale * item.CarryScale;
                var pose = rack.SlotPose(slot);
                busy.Add(item);
                if (proposed) StartCoroutine(PlaceMotion(item, pose, i * .14f));
                else { Sound(placeClip); item.Travel(pose.position, pose.rotation, () => FinishPlace(item, false)); }
            }
            ShowHeld();
            SetStatus(count > 1 ? "Batch: same-type items travel to existing rack slots." : "Single item: compare path and sound at contact.");
        }
        IEnumerator PlaceMotion(SortItem item, Pose pose, float delay)
        {
            item.hitbox.enabled = false; item.gameObject.SetActive(false);
            if (delay > 0) yield return new WaitForSeconds(delay);
            item.gameObject.SetActive(true);
            Vector3 from = hand.position, scale = item.originalScale * item.CarryScale;
            Quaternion rotation = hand.rotation;
            float t = 0;
            while (t < 1)
            {
                t = Mathf.Min(1, t + Time.deltaTime / .38f);
                float u = t * t * (3 - 2 * t);
                item.transform.position = Vector3.Lerp(from, pose.position, u) + Vector3.up * (4 * .18f * t * (1 - t));
                item.transform.rotation = Quaternion.Slerp(rotation, pose.rotation, u) * Quaternion.Euler(new Vector3(6, 25, -8) * Mathf.Sin(t * Mathf.PI));
                item.transform.localScale = Vector3.Lerp(scale, item.originalScale, u) * (1 + .035f * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }
            item.Snap(pose.position, pose.rotation);
            FinishPlace(item, true);
        }
        void FinishPlace(SortItem item, bool enhanced)
        {
            busy.Remove(item); CompletedActions++; Arrivals++;
            if (!enhanced) return;
            Sound(placeClip, .5f, 1 + (rack.Count % 4) * .025f);
            Burst(item.visual.bounds.center, false);
            StartCoroutine(Shine(item));
        }
        void ShowHeld()
        {
            if (held.Count == 0) return;
            var item = held[held.Count - 1]; item.gameObject.SetActive(true);
            item.Snap(hand.position, hand.rotation, hand);
        }

        public void Drop()
        {
            if (held.Count == 0) { SetStatus("PICK an item first."); return; }
            if (busy.Count > 0) { SetStatus("Let the current animation finish, then DROP."); return; }
            var item = held[held.Count - 1]; held.RemoveAt(held.Count - 1);
            item.CompleteMotion(); item.transform.SetParent(null, true); item.location = ItemLocation.Loose;
            var bounds = SortRack.RotatedBounds(item.definition, Quaternion.identity);
            Vector3 destination = new Vector3(1.55f + (samples.IndexOf(item) % 2) * .28f, .015f - bounds.min.y, .5f + (samples.IndexOf(item) / 2) * .28f);
            busy.Add(item);
            if (proposed) StartCoroutine(DropMotion(item, destination));
            else item.ThrowWithPhysics(destination, () => FinishDrop(item, false));
            ShowHeld(); SetStatus("DROP: compare release, rotation and landing.");
        }
        IEnumerator DropMotion(SortItem item, Vector3 destination)
        {
            item.hitbox.enabled = false;
            Vector3 from = item.transform.position, scale = item.transform.localScale;
            Quaternion rotation = item.transform.rotation;
            Sound(pickClip, .15f, .7f);
            float t = 0;
            while (t < 1)
            {
                t = Mathf.Min(1, t + Time.deltaTime / .62f);
                item.transform.position = Vector3.Lerp(from, destination, t) + Vector3.up * (4 * .3f * t * (1 - t));
                item.transform.rotation = Quaternion.Slerp(rotation, Quaternion.identity, t) * Quaternion.Euler(new Vector3(14, 85, 22) * Mathf.Sin(t * Mathf.PI));
                item.transform.localScale = Vector3.Lerp(scale, item.originalScale, Mathf.SmoothStep(0, 1, Mathf.Min(1, t / .25f)));
                yield return null;
            }
            item.Snap(destination, Quaternion.identity);
            FinishDrop(item, true);
        }
        void FinishDrop(SortItem item, bool enhanced)
        {
            busy.Remove(item); CompletedActions++; Arrivals++;
            Sound(dropClip, .4f, ThrowFeel.Pitch(item.definition));
            if (enhanced) Burst(item.transform.position, true);
        }

        void Burst(Vector3 position, bool dust)
        {
            if (!proposed || effects == 0) return;
            if (!sparkMaterial) sparkMaterial = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            var go = new GameObject(dust ? "Contact dust" : "Pickup sparkles");
            transient.Add(go); go.transform.position = position;
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = false; main.duration = .4f; main.startLifetime = dust ? .28f : .22f;
            main.startSpeed = dust ? .22f : .4f; main.startSize = dust ? .035f : .012f;
            main.startColor = dust ? new Color(.7f, .78f, .8f, .4f) : new Color(.45f, 1, .85f, .85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World; main.maxParticles = 24;
            main.gravityModifier = .15f;
            var emission = ps.emission; emission.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .035f;
            var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, AnimationCurve.Linear(0, 1, 1, 0));
            ps.GetComponent<ParticleSystemRenderer>().sharedMaterial = sparkMaterial;
            ps.Emit(effects == 2 ? 16 : 6);
            Destroy(go, 1);
        }
        IEnumerator Shine(SortItem item)
        {
            if (effects == 0 || !item.outline) yield break;
            item.outline.enabled = true; item.outline.OutlineColor = new Color(.55f, 1, .85f);
            float t = 0;
            while (t < 1)
            {
                t += Time.deltaTime / .3f;
                item.outline.OutlineWidth = Mathf.Sin(Mathf.Clamp01(t) * Mathf.PI) * (effects == 2 ? 5 : 2.5f);
                yield return null;
            }
            item.outline.enabled = false;
        }

        public void AutoDemo()
        {
            ResetPreview(); autoRunning = true; StartCoroutine(Demo());
        }
        IEnumerator Demo()
        {
            for (int i = 0; i < 4; i++) { PickNext(); yield return new WaitForSeconds(.48f); }
            Drop(); yield return new WaitForSeconds(2f);
            while (held.Count > 0) { Place(); yield return new WaitForSeconds(.65f); }
            yield return new WaitForSeconds(.6f);
            autoRunning = false; SetStatus("Demo complete. Toggle CURRENT / NEW FEEL and replay with the same setup.");
        }
        void Update()
        {
            if (!view || samples.Count == 0) return;
            if (Keyboard.current != null && !autoRunning)
            {
                if (Keyboard.current.eKey.wasPressedThisFrame) PickNext();
                if (Keyboard.current.fKey.wasPressedThisFrame) Place();
                if (Keyboard.current.qKey.wasPressedThisFrame) Drop();
                if (Keyboard.current.rKey.wasPressedThisFrame) ResetPreview();
            }
            if (autoRunning || Mouse.current == null) return;
            var mouse = Mouse.current;
            bool overUI = EventSystem.current && EventSystem.current.IsPointerOverGameObject();
            SortItem target = null;
            if (!overUI && Physics.Raycast(view.ScreenPointToRay(mouse.position.ReadValue()), out var hit, 10))
                target = hit.collider.GetComponentInParent<SortItem>();
            if (target && (target.location == ItemLocation.Hand || busy.Contains(target) || target.Moving)) target = null;
            if (hover != target) { if (hover) hover.Highlight(false); hover = target; if (hover) hover.Highlight(true); }
            if (hover && proposed && hover.outline) hover.outline.OutlineWidth = Mathf.Lerp(hover.outline.OutlineWidth, 3, Time.deltaTime * 18);
            if (target && mouse.leftButton.wasPressedThisFrame) Pick(target);
        }

        void LateUpdate()
        {
            PreviewVisible = false;
            if (!placementPreviewMaterial || held.Count == 0 || busy.Count > 0) return;
            var item = held[held.Count - 1];
            int slot = rack.NextSlot(item.definition);
            if (slot < 0 || !item.visual) return;
            var filter = item.visual.GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) return;
            // Same mesh, material and next-slot calculation as production RackPlacementPreview.
            var pose = rack.SlotPoseFor(item.definition, slot);
            var relative = item.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var matrix = Matrix4x4.TRS(pose.position, pose.rotation, item.originalScale) * relative;
            for (int i = 0; i < filter.sharedMesh.subMeshCount; i++)
                Graphics.DrawMesh(filter.sharedMesh, matrix, placementPreviewMaterial, gameObject.layer, view, i, null, UnityEngine.Rendering.ShadowCastingMode.Off, false);
            PreviewVisible = true;
        }

        void BuildUI()
        {
            var canvasGO = new GameObject("Feel preview controls", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            canvasGO.transform.SetParent(transform, false);
            canvasGO.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720); scaler.matchWidthOrHeight = .5f;
            var top = Panel(canvasGO.transform, "Header", new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -61), new Vector2(0, 122));
            Label(top, "INTERACTION FEEL LAB", new Vector2(20, -12), new Vector2(500, 30), 25);
            modeText = Label(top, "", new Vector2(20, -47), new Vector2(1200, 26), 17);
            Label(top, "Original models + rack slots + placement ghost  /  fixed rack target  /  no saved progress", new Vector2(20, -80), new Vector2(1200, 24), 15);
            var bottom = Panel(canvasGO.transform, "Controls", new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 83), new Vector2(0, 166));
            modeButton = Button(bottom, "NEW FEEL", 20, -12, 174, () => SetMode(!proposed));
            fxButton = Button(bottom, "FX: SUBTLE", 204, -12, 174, () => SetEffects((effects + 1) % 3));
            batchButton = Button(bottom, "BATCH: OFF", 388, -12, 174, () => { batch = !batch; UpdateLabels(); });
            speedButton = Button(bottom, "SPEED: 1x", 572, -12, 174, () => { slow = !slow; Time.timeScale = slow ? .5f : 1; UpdateLabels(); });
            Button(bottom, "NEXT MODEL", 756, -12, 174, NextModel);
            Button(bottom, "AUTO DEMO", 940, -12, 174, AutoDemo);
            Button(bottom, "PICK  [E]", 20, -65, 174, PickNext);
            Button(bottom, "PLACE  [F]", 204, -65, 174, Place);
            Button(bottom, "DROP  [Q]", 388, -65, 174, Drop);
            Button(bottom, "RESET  [R]", 572, -65, 174, ResetPreview);
            statusText = Label(bottom, "", new Vector2(20, -122), new Vector2(1210, 32), 17);
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("Preview EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }
        void UpdateLabels()
        {
            if (!modeText) return;
            modeText.text = (proposed ? "NEW FEEL: pop + curve / contact sound / controlled drop" : "CURRENT MOTION: actual DockAt / Travel / physics drop") + "    |    " + templates[model].definition.displayName;
            modeButton.GetComponentInChildren<UnityEngine.UI.Text>().text = proposed ? "NEW FEEL" : "CURRENT";
            fxButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "FX: " + new[] { "OFF", "SUBTLE", "STRONG" }[effects];
            batchButton.GetComponentInChildren<UnityEngine.UI.Text>().text = "BATCH: " + (batch ? "ON" : "OFF");
            batchButton.interactable = proposed;
            speedButton.GetComponentInChildren<UnityEngine.UI.Text>().text = slow ? "SPEED: 0.5x" : "SPEED: 1x";
        }
        static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = min; r.anchorMax = max; r.pivot = new Vector2(.5f, .5f); r.anchoredPosition = position; r.sizeDelta = size;
            go.GetComponent<UnityEngine.UI.Image>().color = new Color(.025f, .055f, .09f, .97f); return r;
        }
        static UnityEngine.UI.Text Label(Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
        {
            // Match the existing game's legacy uGUI font stack; no TMP resource dependency.
            var go = new GameObject("Label", typeof(RectTransform), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false);
            var r = (RectTransform)go.transform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(0, 1); r.anchoredPosition = position; r.sizeDelta = size;
            var text = go.GetComponent<UnityEngine.UI.Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.fontSize = fontSize; text.color = new Color(.88f, .96f, 1); text.text = value; text.raycastTarget = false; return text;
        }
        static UnityEngine.UI.Button Button(Transform parent, string caption, float x, float y, float width, UnityEngine.Events.UnityAction action)
        {
            var r = Panel(parent, caption, new Vector2(0, 1), new Vector2(0, 1), new Vector2(x + width / 2, y - 22), new Vector2(width, 44));
            r.GetComponent<UnityEngine.UI.Image>().color = new Color(.10f, .25f, .30f);
            var button = r.gameObject.AddComponent<UnityEngine.UI.Button>(); button.onClick.AddListener(action);
            var label = Label(r, caption, Vector2.zero, new Vector2(width, 44), 17); label.alignment = TextAnchor.MiddleCenter;
            return button;
        }
        void OnDestroy() { Time.timeScale = 1; if (sparkMaterial) Destroy(sparkMaterial); }
    }
}

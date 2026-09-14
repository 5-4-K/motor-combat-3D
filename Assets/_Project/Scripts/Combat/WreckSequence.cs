using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using MotorCombat.Core;

namespace MotorCombat.Combat
{
    /// <summary>
    /// What a destroyed car does after the killing hit. Health has already
    /// blocked its abilities; this moves it to the Wreck layer (so it phases
    /// through everything but the arena), barrel-rolls and fades the model, and
    /// deactivates the car.
    ///
    /// Visual only: the physics box stays flat and keeps sliding to a stop.
    /// On removal the original materials, transforms and colours are put back,
    /// so a future respawn only has to reactivate the car.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class WreckSequence : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        public WreckConfig config;

        struct Rolled
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
        }

        struct Faded
        {
            public Renderer renderer;
            public int index;
            public Color colour;
        }

        struct Swapped
        {
            public Renderer renderer;
            public Material[] original;
            public ShadowCastingMode shadowCastingMode;
        }

        readonly List<Rolled> _rolled = new List<Rolled>();
        readonly List<Faded> _faded = new List<Faded>();
        readonly List<Swapped> _swapped = new List<Swapped>();
        readonly List<Material> _clones = new List<Material>();

        MaterialPropertyBlock _block;
        Health _health;
        float _elapsed;
        float _halfWidth;
        float _halfHeight;
        bool _running;
        bool _shadowsOff;

        void Awake()
        {
            // Subscribed in Awake: the killing hit may arrive before Start.
            _health = GetComponent<Health>();
            _health.Destroyed += OnDestroyedByDamage;
        }

        void Start()
        {
            if (config == null)
            {
                Debug.LogError($"[MotorCombat] WreckSequence on '{name}' has no WreckConfig assigned — a destroyed car will vanish instantly.", this);
            }
        }

        void OnDestroy()
        {
            if (_health != null) _health.Destroyed -= OnDestroyedByDamage;
            RestoreVisuals();
        }

        void OnDestroyedByDamage(DamageReport report)
        {
            int wreckLayer = PhysicsLayers.Wreck;
            if (wreckLayer >= 0) gameObject.layer = wreckLayer;

            if (config == null)
            {
                gameObject.SetActive(false);
                return;
            }

            var box = GetComponent<BoxCollider>();
            _halfWidth = box != null ? box.size.x * 0.5f : 1f;
            _halfHeight = box != null ? box.size.y * 0.5f : 0.5f;

            CaptureVisuals();
            _elapsed = 0f;
            _shadowsOff = false;
            _running = true;
        }

        void Update()
        {
            if (!_running) return;

            _elapsed += Time.deltaTime;

            float angle = WreckMath.RollAngle(_elapsed, config.rollSeconds, config.rollDegrees);
            Quaternion roll = Quaternion.AngleAxis(angle, Vector3.forward);
            Vector3 lift = Vector3.up * WreckMath.Lift(angle, _halfWidth, _halfHeight);

            // Rotating each child's offset about the root rolls the whole model
            // about the collider centre, whatever the child's own pivot.
            for (int i = 0; i < _rolled.Count; i++)
            {
                Rolled rolled = _rolled[i];
                if (rolled.transform == null) continue;
                rolled.transform.localRotation = roll * rolled.rotation;
                rolled.transform.localPosition = roll * rolled.position + lift;
            }

            float alpha = WreckMath.Alpha(_elapsed, config.fadeSeconds);
            for (int i = 0; i < _faded.Count; i++)
            {
                Faded faded = _faded[i];
                if (faded.renderer == null) continue;

                faded.renderer.GetPropertyBlock(_block, faded.index);
                Color colour = faded.colour;
                colour.a *= alpha;
                _block.SetColor(BaseColorId, colour);
                faded.renderer.SetPropertyBlock(_block, faded.index);
            }

            // Once, not every frame: a solid shadow under a near-invisible car
            // reads as a bug, so drop it as soon as the fade crosses the cutoff.
            if (!_shadowsOff && !WreckMath.CastsShadow(alpha))
            {
                SetShadowsOff();
                _shadowsOff = true;
            }

            if (_elapsed >= config.removeAfterSeconds)
            {
                _running = false;
                RestoreVisuals();
                gameObject.SetActive(false);
            }
        }

        void CaptureVisuals()
        {
            RestoreVisuals();
            if (_block == null) _block = new MaterialPropertyBlock();

            // Every direct child but the first-person eye: the model, or the
            // placeholder box AND its separate nose marker.
            foreach (Transform child in transform)
            {
                if (child.name == CarController.DriverAnchorName) continue;
                _rolled.Add(new Rolled { transform = child, position = child.localPosition, rotation = child.localRotation });
            }

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Material[] original = renderer.sharedMaterials;
                var copies = new Material[original.Length];

                for (int i = 0; i < original.Length; i++)
                {
                    if (original[i] == null) continue;

                    copies[i] = new Material(original[i]);
                    WreckMaterials.MakeTransparent(copies[i]);
                    _clones.Add(copies[i]);

                    // Start from the team tint in the property block when there
                    // is one, so fading never loses the car's colour.
                    renderer.GetPropertyBlock(_block, i);
                    Color colour = _block.HasColor(BaseColorId)
                        ? _block.GetColor(BaseColorId)
                        : copies[i].HasProperty(BaseColorId) ? copies[i].GetColor(BaseColorId) : Color.white;

                    _faded.Add(new Faded { renderer = renderer, index = i, colour = colour });
                }

                _swapped.Add(new Swapped { renderer = renderer, original = original, shadowCastingMode = renderer.shadowCastingMode });
                renderer.sharedMaterials = copies;
            }
        }

        void SetShadowsOff()
        {
            for (int i = 0; i < _swapped.Count; i++)
            {
                if (_swapped[i].renderer != null) _swapped[i].renderer.shadowCastingMode = ShadowCastingMode.Off;
            }
        }

        void RestoreVisuals()
        {
            for (int i = 0; i < _rolled.Count; i++)
            {
                if (_rolled[i].transform == null) continue;
                _rolled[i].transform.localPosition = _rolled[i].position;
                _rolled[i].transform.localRotation = _rolled[i].rotation;
            }

            if (_block != null)
            {
                for (int i = 0; i < _faded.Count; i++)
                {
                    Faded faded = _faded[i];
                    if (faded.renderer == null) continue;
                    faded.renderer.GetPropertyBlock(_block, faded.index);
                    _block.SetColor(BaseColorId, faded.colour);
                    faded.renderer.SetPropertyBlock(_block, faded.index);
                }
            }

            for (int i = 0; i < _swapped.Count; i++)
            {
                if (_swapped[i].renderer == null) continue;
                _swapped[i].renderer.sharedMaterials = _swapped[i].original;
                _swapped[i].renderer.shadowCastingMode = _swapped[i].shadowCastingMode;
            }

            for (int i = 0; i < _clones.Count; i++)
            {
                if (_clones[i] != null) Destroy(_clones[i]);
            }

            _rolled.Clear();
            _faded.Clear();
            _swapped.Clear();
            _clones.Clear();
        }
    }
}

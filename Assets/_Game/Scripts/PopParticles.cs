using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>
    /// Вспышка + конфетти при лопании пузырей. Мировые SpriteRenderer'ы из пула
    /// фиксированного размера (ринг-буфер). Создаётся GameManager, вызывается из BoardManager.
    /// </summary>
    public class PopParticles : MonoBehaviour
    {
        public static PopParticles Instance { get; private set; }

        const int Pool = 120;

        struct P
        {
            public Transform tr;
            public SpriteRenderer sr;
            public Vector3 vel;
            public float life, maxLife, baseScale, endScale, rot, spin;
            public bool active;
            public int kind;           // 0 = конфетти (гравитация), 1 = кольцо-вспышка
        }

        P[] _p;
        int _next;
        Camera _cam;
        Sprite _circleSpr, _sparkleSpr, _ringSpr;

        static readonly Color Gold = new Color(1f, 0.84f, 0.20f);

        void Awake()
        {
            Instance = this;
            _cam = Camera.main;
            _circleSpr  = SpriteFactory.Circle();
            _sparkleSpr = SpriteFactory.Sparkle();
            _ringSpr    = SpriteFactory.Ring();

            _p = new P[Pool];
            for (int i = 0; i < Pool; i++)
            {
                var go = new GameObject("Particle");
                go.transform.SetParent(transform);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = _circleSpr;
                sr.sortingOrder = 2;          // над пузырями (1), под снарядом (3)
                go.SetActive(false);
                _p[i] = new P { tr = go.transform, sr = sr };
            }
        }

        ref P Take()
        {
            int i = _next % Pool;
            _next++;
            return ref _p[i];
        }

        public void Burst(Vector3 pos, Color color, int count)
        {
            {
                ref P p = ref Take();
                p.kind = 1;
                p.sr.sprite = _ringSpr;
                p.sr.color = Color.Lerp(Color.white, color, 0.35f);
                p.vel = Vector3.zero;
                p.life = 0f; p.maxLife = 0.28f;
                p.baseScale = 0.5f; p.endScale = 2.3f;
                p.rot = 0f; p.spin = 0f;
                p.active = true;
                p.tr.position = pos;
                p.tr.localScale = Vector3.one * p.baseScale;
                p.tr.rotation = Quaternion.identity;
                p.tr.gameObject.SetActive(true);
            }

            int n = Mathf.Clamp(7 + count, 7, 20);
            for (int k = 0; k < n; k++)
            {
                bool star = (k % 5) < 2;           // ~40% — искорки
                ref P p = ref Take();
                p.kind = 0;
                p.sr.sprite = star ? _sparkleSpr : _circleSpr;

                float ang = Random.value * Mathf.PI * 2f;
                float spd = Random.Range(2.5f, 6.8f);
                p.vel = new Vector3(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd + 1.6f, 0f);
                p.life = 0f;
                p.maxLife = Random.Range(0.40f, 0.70f);
                p.baseScale = star ? Random.Range(0.13f, 0.26f) : Random.Range(0.10f, 0.20f);
                p.rot = Random.value * 360f;
                p.spin = Random.Range(-540f, 540f);
                p.active = true;

                p.tr.position = pos;
                p.tr.localScale = Vector3.one * p.baseScale;
                p.tr.rotation = Quaternion.Euler(0f, 0f, p.rot);
                p.sr.color = star ? (Random.value < 0.5f ? Gold : Color.white)
                                  : new Color(color.r, color.g, color.b, 1f);
                p.tr.gameObject.SetActive(true);
            }
        }

        public void WinBurst()
        {
            Color[] cols = { new Color(1f,0.9f,0.2f), new Color(0.4f,1f,0.5f),
                             new Color(0.5f,0.8f,1f), new Color(1f,0.5f,0.8f),
                             new Color(1f,0.7f,0.3f) };
            if (_cam == null) _cam = Camera.main;
            float topY = _cam != null ? _cam.orthographicSize + 0.5f : 5f;

            for (int k = 0; k < 56; k++)
            {
                bool star = (k % 3) == 0;
                ref P p = ref Take();
                p.kind = 0;
                p.sr.sprite = star ? _sparkleSpr : _circleSpr;

                float x   = Random.Range(-3.5f, 3.5f);
                float spd = Random.Range(2f, 5f);
                float ang = Random.Range(200f, 340f) * Mathf.Deg2Rad;
                p.vel      = new Vector3(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd, 0f);
                p.life     = -Random.Range(0f, 0.4f); // stagger spawn
                p.maxLife  = Random.Range(0.7f, 1.2f);
                p.baseScale= Random.Range(0.14f, 0.28f);
                p.rot      = Random.value * 360f;
                p.spin     = Random.Range(-360f, 360f);
                p.active   = true;

                p.tr.position    = new Vector3(x, topY, 0f);
                p.tr.localScale  = Vector3.one * p.baseScale;
                p.tr.rotation    = Quaternion.Euler(0f, 0f, p.rot);
                p.sr.color       = star ? Gold : cols[k % cols.Length];
                p.tr.gameObject.SetActive(true);
            }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < Pool; i++)
            {
                ref P p = ref _p[i];
                if (!p.active) continue;

                p.life += dt;
                if (p.life < 0f) continue; // stagger delay not elapsed yet
                float t = p.life / p.maxLife;
                if (t >= 1f) { p.active = false; p.tr.gameObject.SetActive(false); continue; }

                if (p.kind == 1)
                {
                    // кольцо: быстрый ease-out рост + затухание
                    float e = 1f - (1f - t) * (1f - t);
                    float s = Mathf.Lerp(p.baseScale, p.endScale, e);
                    p.tr.localScale = new Vector3(s, s, 1f);
                    var rc = p.sr.color; rc.a = 1f - t; p.sr.color = rc;
                }
                else
                {
                    p.vel += Vector3.down * 9f * dt;             // гравитация
                    p.tr.position += p.vel * dt;
                    p.rot += p.spin * dt;
                    p.tr.rotation = Quaternion.Euler(0f, 0f, p.rot);
                    float s = p.baseScale * (1f - t * 0.5f);     // лёгкое сжатие
                    p.tr.localScale = new Vector3(s, s, 1f);
                    var c = p.sr.color; c.a = 1f - t; p.sr.color = c;
                }
            }
        }
    }
}

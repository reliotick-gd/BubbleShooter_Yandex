using System.Collections.Generic;
using UnityEngine;

namespace CozyAnimalTown
{
    /// <summary>Object-pool для пузырей — устраняет GC-паузы от Instantiate/Destroy на каждый выстрел (критично для WebGL).</summary>
    public class BubblePool : MonoBehaviour
    {
        const int PrewarmCount = 150; // 10 рядов × 11 ячеек + запас

        static BubblePool _instance;
        public static BubblePool Instance => _instance;

        readonly Queue<GameObject> _pool = new Queue<GameObject>(PrewarmCount);
        int _created;   // сколько объектов всего создано — для уникальных имён при доращивании

        void Awake()
        {
            _instance = this;
            for (int i = 0; i < PrewarmCount; i++)
                _pool.Enqueue(CreateItem(_created++));
        }

        GameObject CreateItem(int id)
        {
            var go = new GameObject($"BP_{id}");
            go.transform.SetParent(transform);
            go.AddComponent<SpriteRenderer>();
            go.AddComponent<Bubble>();


            go.SetActive(false);
            return go;
        }

        public GameObject Get()
        {
            var go = _pool.Count > 0 ? _pool.Dequeue() : CreateItem(_created++);
            go.SetActive(true);
            return go;
        }

        public void Return(GameObject go)
        {
            if (go == null) return;
            go.SetActive(false);
            go.transform.SetParent(transform);
            go.transform.localScale = Vector3.one; // сброс масштаба от анимации
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr) sr.color = Color.white;
            _pool.Enqueue(go);
        }
    }
}

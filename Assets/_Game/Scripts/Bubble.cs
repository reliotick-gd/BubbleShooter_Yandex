using UnityEngine;

namespace CozyAnimalTown
{
    public class Bubble : MonoBehaviour
    {
        public int colorId;
        public SpriteRenderer sr;
        public int hp;   // для разрушимых препятствий (лёд/ящик); 0/‑1 для остальных
    }
}

using UnityEngine;
using Audio;

namespace Hazard
{
    public class TimedButton : PressableButton
    {
        public Sprite beforeHitting;
        public Sprite afterHitting;
        public bool pressed;
        public float pressedCooldown = 4f;
        private float _timer = 0f;
        private AudioManager _audioManager;

        [SerializeField] public Openable toOpen;

        private SpriteRenderer _sprite;

        private void Start()
        {
            _audioManager = GetComponent<AudioManager>();
            _timer = pressedCooldown;
            _sprite = GetComponent<SpriteRenderer>();
            _sprite.sprite = beforeHitting;
        }

        void Update()
        {
            if (pressed)
            {
                _timer -= Time.deltaTime;
            }

            if (_timer <= 0)
            {
                NormalSprite();
            }
        }

        private void HitSprite()
        {
            _sprite.sprite = afterHitting;
            pressed = true;
            toOpen.Open();
        }

        private void NormalSprite()
        {
            _sprite.sprite = beforeHitting;
            pressed = false;
            toOpen.Close();
        }

        public override void Press()
        {
            _timer = pressedCooldown;
            HitSprite();
            _audioManager.Play("Button");
        }

        public override void UnPress()
        {
            _timer = 0;
            NormalSprite();
        }
    }
}
using System;
using UnityEngine;
using Audio;

namespace Player
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Run and Jump")] public float normalJumpForce;
        public float boostedJumpForce;
        public float jumpTime;
        public float normalMoveSpeed;
        public float boostedMoveSpeed;
        public int numberOfMidairJumps;
        public float somersaultThreshold;
        private float _currentJumpForce;
        public float currentJumpTimer;
        private float _currentMoveSpeed;
        public float currentMoveTimer;

        [Header("Dash")] public float dashCooldown;
        public float dashSpeed;
        public float startDashTime;

        [Header("Physics Checks (Triggers)")] public float wallJumpWindow;
        public ContactTrigger feetTrigger;
        public ContactTrigger leftTrigger;
        public ContactTrigger rightTrigger;

        [Header("Physics Checks (Raycasts)")] public float detectionRange;
        public float ledgeOffsetX1;
        public float ledgeOffsetY1;
        public float ledgeOffsetX2;
        public float ledgeOffsetY2;
        public LayerMask whatIsLedge;
        public Transform wallRayOrigin;
        public Transform bottomLedgeRayOrigin;
        public Transform topLedgeRayOrigin;

        public float ledgeDropWindow;
        public float ledgeFreezeControlsWindow;
        public float midAirMorphWindow;
        public float climbDuration;

        private bool _canClimbLedge;
        private bool _canClimbLedgeMorph;
        private bool _canDetectLedge;
        private bool _canWallJump;
        private bool _dashingLeft;
        private bool _dashingRight;
        private bool _detectedWall;
        private bool _detectedLedgeBottom;
        private bool _detectedLedgeTop;
        private bool _facingRight;
        public bool isClimbing;
        private bool _isGrounded;
        private bool _isHuggingWallLeft;
        private bool _isHuggingWallRight;
        private bool _isJumping;
        private bool _isSomersaulting;
        private bool _fakeMidairCrouch;
        private bool _ledgeDetected;
        private bool _ledgeDetectedMorph;


        private float _dashCooldownLeft;
        private float _dashTime;
        private float _jumpTimeCounter;

        private int _previousDownFrames;
        private int _previousJumpFrames;
        private int _previousUpFrames;
        private int _midairJumps;


        private Rigidbody2D _rb;
        private Vector2 _ledgePosBottom;
        private Vector2 _ledgePos1;
        private Vector2 _ledgePos2;
        
        private AudioManager _audioManager;

        private void Awake()
        {
            feetTrigger.StartedContactEvent += () =>
            {
                _canDetectLedge = true;
                _isGrounded = true;
                _midairJumps = 0;
                _isJumping = false;
                _isSomersaulting = false;
            };
            feetTrigger.StoppedContactEvent += () => { _isGrounded = false; };
            leftTrigger.StartedContactEvent += () =>
            {
                _isHuggingWallLeft = true;
                _dashingLeft = false;
                _dashingRight = false;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            };
            leftTrigger.StoppedContactEvent += () => { _isHuggingWallLeft = false; };
            rightTrigger.StartedContactEvent += () =>
            {
                _isHuggingWallRight = true;
                _dashingLeft = false;
                _dashingRight = false;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            };
            rightTrigger.StoppedContactEvent += () => { _isHuggingWallRight = false; };
        }

        private void OnEnable()
        {
            feetTrigger.enabled = true;
            leftTrigger.enabled = true;
            rightTrigger.enabled = true;
        }

        private void OnDisable()
        {
            feetTrigger.enabled = false;
            leftTrigger.enabled = false;
            rightTrigger.enabled = false;
        }


        // Start is called before the first frame update
        private void Start()
        {
            _rb = GetComponent<Rigidbody2D>();
            _audioManager = GetComponent<AudioManager>();
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            _canDetectLedge = true;
            _dashCooldownLeft = 0;
            _dashTime = startDashTime;
            _facingRight = true;
            _midairJumps = numberOfMidairJumps;
        }

        private void Update()
        {
            CheckSurroundings();
            CheckLedgeClimb();
            _dashCooldownLeft -= Time.deltaTime;
            PlayerEntity.Instance.facingRight = _facingRight;

            currentJumpTimer -= Time.deltaTime;
            if (currentJumpTimer < 0)
            {
                _currentJumpForce = normalJumpForce;
            }
            else
            {
                _currentJumpForce = boostedJumpForce;
            }

            currentMoveTimer -= Time.deltaTime;
            if (currentMoveTimer < 0)
            {
                _currentMoveSpeed = normalMoveSpeed;
            }
            else
            {
                _currentMoveSpeed = boostedMoveSpeed;
            }
            
            _isSomersaulting = !_isGrounded && Math.Abs(_rb.velocity.x) >= somersaultThreshold;

            PlayerEntity.Instance.animators[0].SetBool("Dashing", _dashingLeft || _dashingRight);

            PlayerEntity.Instance.animators[0].SetBool("Jumping", _rb.velocity.y > 0.1f );
            PlayerEntity.Instance.animators[0].SetBool("Falling", _rb.velocity.y < -0.1f && !_isGrounded);
            PlayerEntity.Instance.animators[0].SetBool("Sommersaulting", _isSomersaulting);
            PlayerEntity.Instance.animators[0].SetBool("HuggingWall", ReadyToWallJump());

            PlayerEntity.Instance.animators[0]
                .SetBool("GrabbingLedge", !isClimbing && (_canClimbLedge || _canClimbLedgeMorph));
            PlayerEntity.Instance.animators[0]
                .SetBool("Grounded", _isGrounded);
            PlayerEntity.Instance.animators[0]
                .SetFloat("WalkSpeed", _rb.velocity.magnitude);
        }

        //This function is called every FixedUpdate on PlayerControls
        public void Move(float xInput, float yInput, bool jump, bool dash)
        {
            if (HasToFlip(xInput))
            {
                Flip();
            }

            if (Dashing() && _dashTime < 0)
            {
                _dashingRight = false;
                _dashingLeft = false;
                _dashTime = startDashTime;
                _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
                _dashCooldownLeft = dashCooldown;
            }

            if (CanJump())
            {
                _midairJumps = numberOfMidairJumps;
            }

            if (!HangingToLedge())
            {
                if (CanWallJump())
                {
                    _canWallJump = true;
                }
                else
                {
                    if (_canWallJump) Invoke(nameof(StopWallJump), wallJumpWindow);
                }
            }

            if (dash && _dashCooldownLeft < 0)
            {
                
                _audioManager.Play("Dashing");
                if (PlayerEntity.Instance.isCrouched)
                {
                    Uncrouch();
                }

                if (Math.Abs(xInput) <= 0.1f)
                {
                    //do nothing
                }
                else if (xInput > 0)
                {
                    _dashingRight = true;
                    _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
                }
                else if (xInput < 0)
                {
                    _dashingLeft = true;
                    _rb.constraints = RigidbodyConstraints2D.FreezeRotation | RigidbodyConstraints2D.FreezePositionY;
                }
            }

            if (_dashingRight)
            {
                _dashTime -= Time.deltaTime;
                _rb.velocity = Vector2.right * dashSpeed;
            }
            else if (_dashingLeft)
            {
                _dashTime -= Time.deltaTime;
                _rb.velocity = Vector2.left * dashSpeed;
            }

            if (yInput < 0)
            {
                _previousDownFrames++;
                if (_previousDownFrames == 1 && CanCrouch() && Math.Abs(xInput) <= 0.1f)
                {
                    Crouch();
                }
                else if (_previousDownFrames == 1 && !_isGrounded && !CanMorph(xInput))
                {
                    _fakeMidairCrouch = true;
                    Invoke(nameof(ResetMidAirMorphWindow), midAirMorphWindow);
                }
                else if (_previousDownFrames == 1 && CanMorph(xInput))
                {
                    Morph();
                }
            }
            else if (yInput > 0 && Math.Abs(xInput) <= 0.1f)
            {
                _previousUpFrames++;
                if (_previousUpFrames == 1 && PlayerEntity.Instance.isCrouched)
                {
                    Uncrouch();
                }
                else if (_previousUpFrames == 1 && PlayerEntity.Instance.isMorphed)
                {
                    if (_isGrounded)
                    {
                        Crouch();
                    }
                    else
                    {
                        Uncrouch();
                    }
                }
            }
            else
            {
                _previousDownFrames = 0;
                _previousUpFrames = 0;
            }

            if (jump && !Dashing())
            {
                if (HangingToLedge() && _previousJumpFrames == 0)
                {
                    if ((_facingRight && xInput < 0) || (!_facingRight && xInput > 0))
                    {
                        _canDetectLedge = false;
                        Invoke(nameof(ReCheckLedges), ledgeDropWindow);
                        _canClimbLedge = false;
                        _canClimbLedgeMorph = false;
                        _ledgeDetected = false;
                        _ledgeDetectedMorph = false;
                        //_isSomersaulting = true;
                        _isJumping = true;
                        _jumpTimeCounter = jumpTime;
                        _rb.velocity = Vector2.up * _currentJumpForce;
                    }
                    else if ((_facingRight && xInput > 0) || (!_facingRight && xInput < 0))
                    {
                        KillMomentum();
                        isClimbing = true;
                        if (_canClimbLedgeMorph) PlayerEntity.Instance.animators[0].SetTrigger("ClimbLedgeMorph");
                        if (_canClimbLedge) PlayerEntity.Instance.animators[0].SetTrigger("ClimbLedge");
                        Invoke(nameof(FinishLedgeClimb), climbDuration);
                    }
                    else
                    {
                        _canDetectLedge = false;
                        Invoke(nameof(ReCheckLedges), ledgeDropWindow);
                        _canClimbLedge = false;
                        _canClimbLedgeMorph = false;
                        _ledgeDetected = false;
                        _ledgeDetectedMorph = false;
                        //_isSomersaulting = false;
                        _isJumping = true;
                        _jumpTimeCounter = 0;
                        _rb.velocity = Vector2.zero;
                    }

                    _previousJumpFrames++;
                    return;
                }

                //salto no chão/parede
                if (CanJump() && _previousJumpFrames == 0)
                {
                    if (PlayerEntity.Instance.isCrouched)
                    {
                        Uncrouch();
                    }

                    if (Math.Abs(xInput) >= 0.1)
                    {
                        //_isSomersaulting = true;
                    }

                    _isJumping = true;
                    _jumpTimeCounter = jumpTime;
                    _rb.velocity = Vector2.up * _currentJumpForce;
                }
                //salto no ar
                else if (PlayerEntity.Instance.unlockedDoubleJump && _midairJumps > 0 && _previousJumpFrames == 0)
                {
                    if (Math.Abs(xInput) >= 0.1)
                    {
                        //_isSomersaulting = true;
                    }

                    _midairJumps--;
                    _isJumping = true;
                    _jumpTimeCounter = jumpTime;
                    _rb.velocity = Vector2.zero;
                    _rb.velocity = Vector2.up * _currentJumpForce;
                }
                //fazer o salto mais alto conforme o input
                else if (_isJumping)
                {
                    if (_jumpTimeCounter > 0)
                    {
                        _rb.velocity = Vector2.up * _currentJumpForce;
                        _jumpTimeCounter -= Time.deltaTime;
                    }
                    else
                    {
                        _isJumping = false;
                        //_isSomersaulting = false;
                    }
                }

                _previousJumpFrames++;
            }
            else
            {
                _isJumping = false;
                _previousJumpFrames = 0;
            }

            if (!Dashing() && !HangingToLedge())
            {
                if (PlayerEntity.Instance.isCrouched && Math.Abs(xInput) >= 0.1f)
                {
                    Uncrouch();
                }

                _rb.velocity = new Vector2(_currentMoveSpeed * xInput, _rb.velocity.y);
            }
        }

        public void KillMomentum()
        {
            _rb.velocity = Vector2.zero;
        }

        private void Crouch()
        {
            PlayerEntity.Instance.isCrouched = true;
            PlayerEntity.Instance.isMorphed = false;
        }

        private void Uncrouch()
        {
            PlayerEntity.Instance.isCrouched = false;
        }

        private void Morph()
        {
            if (PlayerEntity.Instance.unlockedMorphBall)
            {
                _fakeMidairCrouch = false;
                PlayerEntity.Instance.isCrouched = false;
                PlayerEntity.Instance.isMorphed = true;
            }
        }

        private void ResetMidAirMorphWindow()
        {
            _fakeMidairCrouch = false;
        }

        public void FinishLedgeClimb()
        {
            if (_canClimbLedgeMorph)
            {
                if (!PlayerEntity.Instance.unlockedMorphBall) return;
                Morph();
            }

            KillMomentum();
            //if (_canClimbLedgeMorph) PlayerEntity.Instance.animators[0].ResetTrigger("ClimbLedgeMorph");
            //if (_canClimbLedge) PlayerEntity.Instance.animators[0].ResetTrigger("ClimbLedge");
            _rb.constraints = RigidbodyConstraints2D.FreezeAll;
            PlayerEntity.Instance.frozeControls = true;
            transform.position = _ledgePos2;
            _canClimbLedge = false;
            _canClimbLedgeMorph = false;
            _ledgeDetected = false;
            _ledgeDetectedMorph = false;
            Invoke(nameof(RegainControl), ledgeFreezeControlsWindow);
        }

        private bool HasToFlip(float xInput)
        {
            return !_canClimbLedgeMorph && !_canClimbLedge && ((_facingRight && (xInput < 0 || _dashingLeft)
                                                                || (!_facingRight && (xInput > 0 || _dashingRight))));
        }

        private bool CanWallJump()
        {
            return _isSomersaulting && ((_facingRight && _isHuggingWallRight) || (!_facingRight && _isHuggingWallLeft));
        }

        private void Flip()
        {
            _facingRight = !_facingRight;
            Vector3 scaler = transform.localScale;
            scaler.x *= -1;
            transform.localScale = scaler;
        }

        private bool CanJump()
        {
            return _isGrounded || ReadyToWallJump();
        }

        private bool ReadyToWallJump()
        {
            return (_canWallJump && ((_facingRight && _isHuggingWallLeft) || (!_facingRight && _isHuggingWallRight)));
        }

        private bool CanCrouch()
        {
            return _isGrounded && !PlayerEntity.Instance.isCrouched && !Dashing() && !HangingToLedge();
        }

        private bool CanMorph(float xInput)
        {
            return ((_isGrounded && PlayerEntity.Instance.isCrouched && Math.Abs(xInput) <= 0.1f) ||
                    (!_isGrounded && _fakeMidairCrouch)) &&
                   !PlayerEntity.Instance.isMorphed && !Dashing() && !HangingToLedge();
        }

        private bool Dashing()
        {
            return _dashingLeft || _dashingRight;
        }

        private bool HangingToLedge()
        {
            return _canClimbLedge || _canClimbLedgeMorph;
        }

        private void CheckSurroundings()
        {
            if (!_canDetectLedge || HangingToLedge() || PlayerEntity.Instance.isCrouched ||
                PlayerEntity.Instance.isMorphed)
            {
                _detectedWall = false;
                _detectedLedgeBottom = false;
                _detectedLedgeTop = false;
                return;
            }

            if (_facingRight)
            {
                _detectedWall = Physics2D.Raycast(wallRayOrigin.position, transform.right, detectionRange, whatIsLedge);
                _detectedLedgeBottom =
                    Physics2D.Raycast(bottomLedgeRayOrigin.position, transform.right, detectionRange, whatIsLedge);
                _detectedLedgeTop =
                    Physics2D.Raycast(topLedgeRayOrigin.position, transform.right, detectionRange, whatIsLedge);
            }
            else
            {
                _detectedWall =
                    Physics2D.Raycast(wallRayOrigin.position, transform.right * -1, detectionRange, whatIsLedge);
                _detectedLedgeBottom =
                    Physics2D.Raycast(bottomLedgeRayOrigin.position, transform.right * -1, detectionRange, whatIsLedge);
                _detectedLedgeTop =
                    Physics2D.Raycast(topLedgeRayOrigin.position, transform.right * -1, detectionRange, whatIsLedge);
            }

            if (_detectedWall && !_detectedLedgeBottom && _detectedLedgeTop && !_ledgeDetectedMorph)
            {
                _ledgeDetectedMorph = true;
                _ledgeDetected = false;
                _ledgePosBottom = wallRayOrigin.position;
            }
            else if (!_detectedLedgeBottom && _detectedWall && !_detectedLedgeBottom && !_detectedLedgeTop &&
                     !_ledgeDetected)
            {
                _ledgeDetected = true;
                _ledgeDetectedMorph = false;
                _ledgePosBottom = wallRayOrigin.position;
            }
        }

        private void CheckLedgeClimb()
        {
            if (!_canDetectLedge || PlayerEntity.Instance.isMorphed || PlayerEntity.Instance.isCrouched) return;

            if (_ledgeDetectedMorph && !_canClimbLedgeMorph)
            {
                _canClimbLedgeMorph = true;
                _canClimbLedge = false;
            }

            if (!_canClimbLedgeMorph && _ledgeDetected && !_canClimbLedge)
            {
                _canClimbLedge = true;
                _canClimbLedgeMorph = false;
            }

            if (_facingRight)
            {
                _ledgePos1 = new Vector2(Mathf.Floor(_ledgePosBottom.x + detectionRange) - ledgeOffsetX1,
                    Mathf.Floor(_ledgePosBottom.y) + ledgeOffsetY1);
                _ledgePos2 = new Vector2(Mathf.Floor(_ledgePosBottom.x + detectionRange) + ledgeOffsetX2,
                    Mathf.Floor(_ledgePosBottom.y) + ledgeOffsetY2);
            }
            else
            {
                _ledgePos1 = new Vector2(Mathf.Floor(_ledgePosBottom.x) + ledgeOffsetX1,
                    Mathf.Floor(_ledgePosBottom.y) + ledgeOffsetY1);
                _ledgePos2 = new Vector2(Mathf.Floor(_ledgePosBottom.x) - ledgeOffsetX2,
                    Mathf.Floor(_ledgePosBottom.y) + ledgeOffsetY2);
            }

            if (_canClimbLedge || _canClimbLedgeMorph)
            {
                transform.position = _ledgePos1;
            }
        }

        private void StopWallJump()
        {
            _canWallJump = false;
        }

        private void ReCheckLedges()
        {
            _canDetectLedge = true;
        }

        private void RegainControl()
        {
            isClimbing = false;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            PlayerEntity.Instance.frozeControls = false;
        }

        public void IncreaseJumpTimer(float time)
        {
            if (currentJumpTimer < 0)
            {
                currentJumpTimer = 0;
            }

            currentJumpTimer = Mathf.Clamp(currentMoveTimer + time, 0, PlayerEntity.Instance.maxJumpBuffTime);
        }

        public void IncreaseMoveTimer(float time)
        {
            if (currentMoveTimer < 0)
            {
                currentMoveTimer = 0;
            }

            currentMoveTimer = Mathf.Clamp(currentMoveTimer + time, 0, PlayerEntity.Instance.maxSpeedBuffTime);
        }
    }
}
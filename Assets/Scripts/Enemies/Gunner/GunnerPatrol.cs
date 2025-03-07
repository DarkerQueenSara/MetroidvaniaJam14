using System;
using UnityEngine;

namespace Enemies.Gunner
{
    public class GunnerPatrol: GunnerState
    {
        private Vector2 _patrolLeftPoint;
        private Vector2 _patrolRightPoint;

        private Animator _animator;
        public static GunnerPatrol Create(Gunner target)
        {
            GunnerPatrol state = GunnerState.Create<GunnerPatrol>(target);
            return state;
        }

        public override void StateStart()
        {
            base.StateStart();
            _patrolLeftPoint = (Vector2) target.currentPatrolAnchor + Vector2.left * target.horizontalRange;
            _patrolRightPoint = (Vector2) target.currentPatrolAnchor + Vector2.right * target.horizontalRange;
            target.Flip();
            _animator = GetComponent<Animator>();
            _animator.SetBool("Shooting", false);
            _animator.SetBool("Idle", false);
            _animator.SetBool("Walking", true);
        }

        public override void StateFixedUpdate()
        {
            base.StateFixedUpdate();
            if (target.facingRight)
            {
                target.MoveInDirection(Vector2.right);
            }
            else
            {
                target.MoveInDirection(Vector2.left);
            }

            if (target.CheckForPlayer())
            {
                SetState(GunnerChase.Create(target));
            }

            if (target.TouchingWall()|| !target.TouchingGround() || TimeToChange())
            {
                SetState(GunnerIdle.Create(target));
            }
        }

        private bool TimeToChange()
        {
            if (!target.facingRight && Math.Abs(transform.position.x - _patrolLeftPoint.x) <= 0.05f) return true;
            if (target.facingRight && Math.Abs(transform.position.x - _patrolRightPoint.x) <= 0.05f) return true;
            return false;
        }
    }
}

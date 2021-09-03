﻿using EntityStates;
using RoR2;
using RoR2.Audio;
using System;
using UnityEngine;
using UnityEngine.Networking;

namespace SettMod.SkillStates.BaseStates
{
    public class BaseMeleeAttack : BaseSkillState
    {
        public int swingIndex;

        protected string hitboxName = "Sword";

        protected DamageType damageType = DamageType.Generic;
        protected float damageCoefficient = 3.5f;
        protected float procCoefficient = 1f;
        protected float pushForce = 300f;
        protected Vector3 bonusForce = Vector3.zero;


        protected float baseDuration = 0.9f;
        protected float baseEarlyExitTime = 0.58f;
        public static float baseDurationBeforeInterruptable;
        private float durationBeforeInterruptable;

        protected float attackRecoil = 1.15f;
        protected float hitHopVelocity = 4f;

        protected string swingSoundString = "";
        protected string hitSoundString = "";
        protected string muzzleString = "SwingCenter";
        protected GameObject swingEffectPrefab;
        protected GameObject hitEffectPrefab;
        protected NetworkSoundEventIndex impactSound;

        private float earlyExitDuration;
        public float duration;
        private bool hasFired;
        private float hitPauseTimer;
        private OverlapAttack attack;
        protected bool inHitPause;
        private bool hasHopped;
        protected float stopwatch;
        protected Animator animator;
        private BaseState.HitStopCachedState hitStopCachedState;
        private Transform modelBaseTransform;
        private Vector3 storedVelocity;

        public override void OnEnter()
        {
            base.OnEnter();
            base.StartAimMode(2f, false);
            this.hasFired = false;

            base.characterBody.outOfCombatStopwatch = 0f;

            if (this.swingIndex == 0)
            {
                this.baseDuration = 0.7f;
                this.baseEarlyExitTime = 0.48f;
            }
            else if (this.swingIndex == 1)
            {
                this.baseDuration = 1.2f;
                this.baseEarlyExitTime = 0.68f;
                this.damageCoefficient = 5f;
            }

            this.duration = this.baseDuration / this.attackSpeedStat;
            this.earlyExitDuration = this.duration * this.baseEarlyExitTime;

            this.durationBeforeInterruptable = this.duration;

            this.swingEffectPrefab = Modules.Assets.swordSwingEffect;
            this.muzzleString = this.muzzleString = swingIndex % 2 == 0 ? "SwingLeft" : "SwingRight";
            this.swingSoundString = "VOHIT";
            this.hitSoundString = "Hit";
            this.impactSound = Modules.Assets.swordHitSoundEvent.index;

            HitBoxGroup hitBoxGroup = null;

            this.modelBaseTransform = base.GetModelTransform();
            this.animator = base.GetModelAnimator();

            hitBoxGroup = Array.Find<HitBoxGroup>(this.modelBaseTransform.GetComponents<HitBoxGroup>(), (HitBoxGroup element) => element.groupName == this.hitboxName);

            if (this.animator.GetBool("isMoving"))
            {
                base.PlayCrossfade("Gesture, Override", "Slash" + (1 + this.swingIndex), "Slash.playbackRate", this.swingIndex % 2 == 0 ? this.duration : this.duration, 0.05f);
            }
            else
            {
                base.PlayCrossfade("FullBody, Override", "Slash" + (1 + this.swingIndex), "Slash.playbackRate", this.swingIndex % 2 == 0 ? this.duration : this.duration, 0.05f);
            }

            this.attack = new OverlapAttack();
            this.attack.damageType = this.damageType;
            this.attack.attacker = base.gameObject;
            this.attack.inflictor = base.gameObject;
            this.attack.teamIndex = base.GetTeam();
            this.attack.damage = this.damageCoefficient * this.damageStat;
            this.attack.procCoefficient = this.procCoefficient;
            this.attack.hitEffectPrefab = Modules.Assets.swordHitImpactEffect;
            this.attack.forceVector = this.bonusForce;
            this.attack.pushAwayForce = this.pushForce;
            this.attack.hitBoxGroup = hitBoxGroup;
            this.attack.isCrit = base.RollCrit();
            this.attack.impactSound = this.impactSound;
        }

        public override void OnExit()
        {

            if (!this.hasFired) this.FireAttack();
            //this.animator.SetBool("attacking", false);
            //base.PlayAnimation("FullBody, Override", "BufferEmpty");
            base.OnExit();

        }

        private void FireAttack()
        {
            if (!this.hasFired)
            {
                this.hasFired = true;

                Util.PlayAttackSpeedSound(this.swingSoundString, base.gameObject, this.attackSpeedStat);

                EffectManager.SimpleMuzzleFlash(this.swingEffectPrefab, base.gameObject, this.muzzleString, true);
                base.AddRecoil(-1f * this.attackRecoil, -2f * this.attackRecoil, -0.5f * this.attackRecoil, 0.5f * this.attackRecoil);
            }

            if (base.isAuthority)
            {
                if (this.attack.Fire())
                {
                    Util.PlaySound(this.hitSoundString, base.gameObject);

                    if (!this.hasHopped)
                    {
                        if (base.characterMotor && !base.characterMotor.isGrounded)
                        {
                            base.SmallHop(base.characterMotor, this.hitHopVelocity);
                        }

                        this.hasHopped = true;
                    }

                    if (!this.inHitPause)
                    {
                        this.storedVelocity = base.characterMotor.velocity;
                        this.hitStopCachedState = base.CreateHitStopCachedState(base.characterMotor, this.animator, "Slash.playbackRate");
                        this.hitPauseTimer = (1.5f * EntityStates.Merc.GroundLight.hitPauseDuration) / this.attackSpeedStat;
                        this.inHitPause = true;
                    }
                }
            }
        }

        protected virtual void SetNextState()
        {

            int index = this.swingIndex;
            if (index == 0) index = 1;
            else index = 0;

            this.outer.SetNextState(new BaseMeleeAttack
            {
                swingIndex = index
            });
        }

        public override void FixedUpdate()
        {
            base.FixedUpdate();

            this.hitPauseTimer -= Time.fixedDeltaTime;

            if (this.hitPauseTimer <= 0f && this.inHitPause)
            {
                base.ConsumeHitStopCachedState(this.hitStopCachedState, base.characterMotor, this.animator);
                this.inHitPause = false;
                base.characterMotor.velocity = this.storedVelocity;
            }

            if (!this.inHitPause)
            {
                this.stopwatch += Time.fixedDeltaTime;
            }
            else
            {
                if (base.characterMotor) base.characterMotor.velocity = Vector3.zero;
                if (this.animator) this.animator.SetFloat("Slash.playbackRate", 0f);
            }

            if (this.stopwatch >= this.duration * 0.2f && this.stopwatch <= this.duration * 0.4)
            {
                this.FireAttack();
            }

            if (this.fixedAge >= this.earlyExitDuration && base.inputBank.skill1.down && base.isAuthority)
            {
                int index = this.swingIndex;
                if (index == 0) index = 1;
                else index = 0;

                this.outer.SetNextState(new BaseMeleeAttack
                {
                    swingIndex = index
                });
                return;
            }

            if (base.fixedAge >= this.duration && base.isAuthority)
            {
                this.outer.SetNextStateToMain();
                return;
            }
        }

        public override InterruptPriority GetMinimumInterruptPriority()
        {
            return InterruptPriority.Skill;
        }

        public override void OnSerialize(NetworkWriter writer)
        {
            base.OnSerialize(writer);
            writer.Write(this.swingIndex);
        }

        public override void OnDeserialize(NetworkReader reader)
        {
            base.OnDeserialize(reader);
            this.swingIndex = reader.ReadInt32();
        }
    }
}
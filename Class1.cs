using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ThunderRoad;
using UnityEngine;

namespace AnimeSlice
{
    public class SliceModule : ItemModule
    {
        public float SliceDamage = 20f;
        public bool SliceDismember = true;
        public bool SpinSlice = true;
        public string ActivationButton = "Alt Use";
        public bool Toggle = false;
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<AnimeSliceComponent>().Setup(SliceDamage, SliceDismember, ActivationButton, SpinSlice, Toggle);
        }
    }
    public class AnimeSliceComponent : MonoBehaviour
    {
        Item item;
        List<RagdollPart> parts = new List<RagdollPart>();
        List<Damager> damagers = new List<Damager>();
        Dictionary<Collider, bool> colliders = new Dictionary<Collider, bool>();
        Dictionary<Breakable, float> breakables = new Dictionary<Breakable, float>();
        float damage;
        bool dismember;
        bool spin;
        bool active = false;
        Interactable.Action onButton;
        Interactable.Action offButton;
        bool toggle;
        public void Awake()
        {
            item = GetComponent<Item>();
            item.OnSnapEvent += Item_OnSnapEvent;
            item.OnHeldActionEvent += Item_OnHeldActionEvent;
            item.OnUngrabEvent += Item_OnUngrabEvent;
            item.OnGrabEvent += Item_OnGrabEvent;
            item.OnTelekinesisReleaseEvent += Item_OnTelekinesisReleaseEvent;
            item.OnTelekinesisGrabEvent += Item_OnTelekinesisGrabEvent;
            item.OnTKSpinStart += Item_OnTKSpinStart;
            item.OnTKSpinEnd += Item_OnTKSpinEnd;
            foreach(Damager damager in item.GetComponentsInChildren<Damager>())
            {
                if(damager.data.damageModifierData.damageType == DamageType.Slash || damager.data.damageModifierData.damageType == DamageType.Pierce)
                {
                    foreach(Collider collider in damager.colliderGroup.colliders)
                    {
                        if (!colliders.ContainsKey(collider))
                            colliders.Add(collider, collider.isTrigger);
                    }
                }
                if(!damagers.Contains(damager))
                damagers.Add(damager);
            }
        }

        private void Item_OnTKSpinEnd(Handle held, bool spinning, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart && active && spin)
            {
                Deactivate();
            }
        }

        private void Item_OnTKSpinStart(Handle held, bool spinning, EventTime eventTime)
        {
            if (eventTime == EventTime.OnStart && !active && spin)
            {
                Activate();
            }
        }

        public void Setup(float sliceDamage = 20f, bool sliceDismember = true, string activationButton = "Alt Use", bool sliceSpin = true, bool toggleSlice = false)
        {
            damage = sliceDamage;
            dismember = sliceDismember;
            spin = sliceSpin;
            if (activationButton.ToLower().Contains("trigger") || activationButton.ToLower() == "use")
            {
                onButton = Interactable.Action.UseStart;
                offButton = Interactable.Action.UseStop;
            }
            else if (activationButton.ToLower().Contains("alt") || activationButton.ToLower().Contains("spell"))
            {
                onButton = Interactable.Action.AlternateUseStart;
                offButton = Interactable.Action.AlternateUseStop;
            }
            else
            {
                onButton = Interactable.Action.AlternateUseStart;
                offButton = Interactable.Action.AlternateUseStop;
            }
            toggle = toggleSlice;
        }

        private void Item_OnTelekinesisReleaseEvent(Handle handle, SpellTelekinesis teleGrabber)
        {
            Deactivate();
        }

        private void Item_OnTelekinesisGrabEvent(Handle handle, SpellTelekinesis teleGrabber)
        {
            Deactivate();
        }

        private void Item_OnGrabEvent(Handle handle, RagdollHand ragdollHand)
        {
            Deactivate();
        }

        private void Item_OnUngrabEvent(Handle handle, RagdollHand ragdollHand, bool throwing)
        {
            Deactivate();
        }

        private void Item_OnHeldActionEvent(RagdollHand ragdollHand, Handle handle, Interactable.Action action)
        {
            if (!toggle)
            {
                if (action == onButton)
                {
                    Activate();
                }
                else if (action == offButton)
                {
                    Deactivate();
                }
            }
            else
            {
                if(action == onButton)
                {
                    if (!active) Activate();
                    else Deactivate();
                }
            }
        }
        public void Activate()
        {
            foreach(Collider collider in colliders.Keys)
            {
                collider.isTrigger = true;
            }
            foreach (Damager damager in damagers)
            {
                damager.UnPenetrateAll();
            }
            active = true;
        }
        public void Deactivate()
        {
            foreach (Collider collider in colliders.Keys)
            {
                collider.isTrigger = colliders[collider];
            }
            foreach(Damager damager in damagers)
            {
                damager.UnPenetrateAll();
            }
            active = false;
        }

        private void Item_OnSnapEvent(Holder holder)
        {
            if (parts != null) StartCoroutine(AnimeSlice());
            Deactivate();
        }
        public IEnumerator AnimeSlice()
        {
            foreach (Breakable breakable in breakables.Keys)
            {
                --breakable.hitsUntilBreak;
                if (breakable.canInstantaneouslyBreak)
                    breakable.hitsUntilBreak = 0;
                breakable.onTakeDamage?.Invoke(breakables[breakable]);
                if (breakable.IsBroken || breakable.hitsUntilBreak > 0)
                    continue;
                breakable.Break();
            }
            breakables.Clear();
            foreach (RagdollPart part in parts)
            {
                if (part?.ragdoll?.creature?.gameObject?.activeSelf == true && part != null && !part.isSliced && part?.ragdoll?.creature != Player.currentCreature)
                {
                    part.gameObject.SetActive(true);
                    CollisionInstance instance = new CollisionInstance(new DamageStruct(DamageType.Slash, damage))
                    {
                        targetCollider = part.colliderGroup.colliders[0],
                        targetColliderGroup = part.colliderGroup,
                        sourceCollider = item.colliderGroups[0].colliders[0],
                        sourceColliderGroup = item.colliderGroups[0],
                        casterHand = item.lastHandler.caster,
                        impactVelocity = item.physicBody.velocity,
                        contactPoint = part.transform.position,
                        contactNormal = -item.physicBody.velocity
                    };
                    instance.damageStruct.hitRagdollPart = part;
                    if (item.colliderGroups[0].imbue.energy > 0 && item.colliderGroups[0].imbue is Imbue imbue)
                    {
                        imbue.spellCastBase.OnImbueCollisionStart(instance);
                        yield return null;
                    }
                    if (part.sliceAllowed && dismember)
                    {
                        part.ragdoll.TrySlice(part);
                        if (part.data.sliceForceKill)
                            part.ragdoll.creature.Kill();
                        yield return null;
                    }
                    part.ragdoll.creature.Damage(instance);
                }
            }
            parts.Clear();
            yield break;
        }
        public void OnTriggerEnter(Collider c)
        {
            if (item.holder == null && c.GetComponentInParent<Breakable>() is Breakable breakable)
            {
                if (!breakables.ContainsKey(breakable) || (breakables.ContainsKey(breakable) && item.physicBody.velocity.sqrMagnitude > breakables[breakable]))
                {
                    breakables.Remove(breakable);
                    breakables.Add(breakable, item.physicBody.velocity.sqrMagnitude);
                }
            }
            if (item.holder == null && c.GetComponentInParent<ColliderGroup>() is ColliderGroup group && group.collisionHandler.isRagdollPart)
            {
                group.collisionHandler.ragdollPart.gameObject.SetActive(true);
                if (!parts.Contains(group.collisionHandler.ragdollPart))
                {
                    parts.Add(group.collisionHandler.ragdollPart);
                }
            }
        }
    }
}

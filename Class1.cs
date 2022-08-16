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
        public override void OnItemLoaded(Item item)
        {
            base.OnItemLoaded(item);
            item.gameObject.AddComponent<AnimeSliceComponent>().Setup(SliceDamage, SliceDismember, ActivationButton, SpinSlice);
        }
    }
    public class AnimeSliceComponent : MonoBehaviour
    {
        Item item;
        List<RagdollPart> parts = new List<RagdollPart>();
        List<Damager> damagers = new List<Damager>();
        Dictionary<Collider, bool> colliders = new Dictionary<Collider, bool>();
        SpellTelekinesis telekinesis;
        float damage;
        bool dismember;
        bool spin;
        bool active = false;
        Interactable.Action onButton;
        Interactable.Action offButton;
        public void Awake()
        {
            item = GetComponent<Item>();
            item.OnSnapEvent += Item_OnSnapEvent;
            item.OnHeldActionEvent += Item_OnHeldActionEvent;
            item.OnUngrabEvent += Item_OnUngrabEvent;
            item.OnGrabEvent += Item_OnGrabEvent;
            item.OnTelekinesisReleaseEvent += Item_OnTelekinesisReleaseEvent;
            item.OnTelekinesisGrabEvent += Item_OnTelekinesisGrabEvent;
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

        public void Setup(float sliceDamage = 20f, bool sliceDismember = true, string activationButton = "Alt Use", bool sliceSpin = true)
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
        }

        private void Item_OnTelekinesisReleaseEvent(Handle handle, SpellTelekinesis teleGrabber)
        {
            telekinesis = null;
            Deactivate();
        }

        private void Item_OnTelekinesisGrabEvent(Handle handle, SpellTelekinesis teleGrabber)
        {
            telekinesis = teleGrabber;
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
            if (action == onButton)
            {
                Activate();
            }
            else if (action == offButton)
            {
                Deactivate();
            }
        }
        public void FixedUpdate()
        {
            if (telekinesis != null && telekinesis.spinMode && !active && spin)
            {
                Activate();
            }
            else if (telekinesis != null && !telekinesis.spinMode && active && spin)
            {
                Deactivate();
            }
        }
        public void Activate()
        {
            foreach(Collider collider in colliders.Keys)
            {
                collider.isTrigger = true;
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
            foreach (RagdollPart part in parts)
            {
                if (part?.ragdoll?.creature?.gameObject?.activeSelf == true && part != null && !part.isSliced && part?.ragdoll?.creature != Player.currentCreature)
                {
                    if (part.sliceAllowed && dismember)
                    {
                        part.ragdoll.TrySlice(part);
                        if (part.data.sliceForceKill)
                            part.ragdoll.creature.Kill();
                        yield return null;
                    }
                    if (!part.ragdoll.creature.isKilled)
                    {
                        CollisionInstance instance = new CollisionInstance(new DamageStruct(DamageType.Slash, damage));
                        instance.damageStruct.hitRagdollPart = part;
                        part.ragdoll.creature.Damage(instance);
                    }
                }
            }
            parts.Clear();
            yield break;
        }
        public void OnTriggerEnter(Collider c)
        {
            if (item.holder == null && c.GetComponentInParent<ColliderGroup>() != null)
            {
                ColliderGroup enemy = c.GetComponentInParent<ColliderGroup>();
                if (enemy?.collisionHandler?.ragdollPart != null && enemy?.collisionHandler?.ragdollPart?.ragdoll?.creature != Player.currentCreature)
                {
                    RagdollPart part = enemy.collisionHandler.ragdollPart;
                    part.gameObject.SetActive(true);
                    if (part.ragdoll.creature != Player.currentCreature && parts.Contains(part) == false)
                    {
                        parts.Add(part);
                    }
                }
            }
        }
    }
}

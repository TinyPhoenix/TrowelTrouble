﻿using TP.ExtensionMethods;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;
using System;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace TP.Greenfab
{

    [Serializable]
    public class PrefabLink : MonoBehaviour
    {
        private GameObject target;
        private bool revertSuccessful;
        private bool dirty;

        //Used by editor
        private float revertStartTime;
        public float updateDirtyStartTime;
        public static float dirtyChecksPerSecond = 1;
        public static bool ChangeNames
        {
            set { ExtensionMethods.ExtensionMethods.includeNames = value; }
            get { return ExtensionMethods.ExtensionMethods.includeNames; }
        }
        public static bool useUnityEditorApply = true;
        public static bool useUnityEditorRevert = false;
        public static string lastPrefabDirectory = "";
        public static bool advancedOptions = false;
        public static bool prefabOnlyOptions = false;
        public static bool debugInfo = false;
        public static bool propogateChanges = false;
        public static bool editorIgnoreTopTransform = true;
        
        public override string ToString()
        {
            string append = "";

            if (gameObject.IsPrefab())
            {
                append = " (Prefab)";
            } 
            else
            {
                append = " (Scene Object)";
            }

            return String.Concat(base.ToString(), append);
        }

        public override bool Equals(object other)
        {
            bool equals = false;

            if (base.Equals(other))
            {
                equals = true;
            }
            else if (other is PrefabLink)
            {
                PrefabLink otherPrefabLink = other as PrefabLink;

                equals = Target == otherPrefabLink.Target;
            }

            return equals;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode() + Target.GetHashCode();
        }

        public void UpdateDirty()
        {
            Dirty = false;

            if (gameObject != null && Target != null)
            {
                Dirty = !gameObject.ValueEquals(Target);
            }
        }
        
        public bool Revert(bool revertChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=false)
        {
            GameObject from = Target;
            GameObject to = gameObject;
            
            Revert(from, to, ignoreTopTransform, ignorePrefabLink);

            if (revertChildren)
            {
                foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(to))
                {
                    directChildprefabLink.Revert(revertChildren, true, ignorePrefabLink);
                }
            }

            return true;
        }

        public void Revert(GameObject from, GameObject to, bool ignoreTopTransform=true, bool ignorePrefabLink=false)
        {
            Copy(from, to, ignoreTopTransform, ignorePrefabLink);
            revertSuccessful = true;
        }

        public bool Apply(bool applyChildren=true, bool ignoreTopTransform=true, bool ignorePrefabLink=false)
        {
            GameObject from = Target;
            GameObject to = gameObject;

            if (applyChildren)
            {
                foreach (PrefabLink directChildprefabLink in DirectChildPrefabLinks(to))
                {
                    directChildprefabLink.Apply(applyChildren, true, ignorePrefabLink);
                }
            }

            GameObject updatedFrom = Apply(from, to, ignoreTopTransform, ignorePrefabLink);
            
            PrefabLink toPrefabLink = to.GetComponent<PrefabLink>();
            if (toPrefabLink != null)
            {
                toPrefabLink.Target = updatedFrom;
            }

            if (updatedFrom != null)
            {
                PrefabLink fromPrefabLink = updatedFrom.GetComponent<PrefabLink>();
                if (fromPrefabLink != null)
                {
                    fromPrefabLink.Target = updatedFrom;
                }
            }

            return true;
        }

        public GameObject Apply(GameObject from, GameObject to, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            return Copy(to, from, ignoreTopTransform, ignorePrefabLink);
        }

        public GameObject Copy(GameObject from, GameObject to, bool ignoreTopTransform = true, bool ignorePrefabLink = true)
        {
            GameObject updatedTo = to;

            if (from != to && from != null && to != null)
            {
                //bool unityEditor = false;
                //#if UNITY_EDITOR
                //    if (to.IsPrefab())
                //    {
                //        unityEditor = true; //Cant modify prefab parrents so have to use unity editor.
                //    }
                //#endif

                //if (unityEditor)
                //{
                //    updatedTo = PrefabUtility.ReplacePrefab(from, to, ReplacePrefabOptions.ConnectToPrefab);
                //}
                //else
                //{
                    RemoveComponentsAndChildren(to, ignoreTopTransform, ignorePrefabLink);
                    CopyComponentsAndChildren(from, to, ignoreTopTransform, ignorePrefabLink);
                //}
            }

            return updatedTo;
        }

        public void RemoveComponentsAndChildren(GameObject from, bool ignoreTopTransform=true, bool ignorePrefabLink=true)
        {
            if (from == null) { from = gameObject; }

            //Remove children
            for (int i = from.transform.childCount - 1; i >= 0; i--)
            {
                RemoveGameObject(from.transform.GetChild(i).gameObject);
            }

            //Remove components
            List<Component> componentsToRemove = from.GetComponents<Component>().ToList();

            while (componentsToRemove.Count > 0)
            {
                Component component = componentsToRemove[0];

                if (ignoreTopTransform && component.GetType() == typeof(Transform) ||
                    ignorePrefabLink && component.GetType() == typeof(PrefabLink))
                {
                    componentsToRemove.Remove(component);
                }
                else
                {
                    List<Component> removed = new List<Component> { };
                    RemoveComponentAndRequiredComponents(component, removed);

                    componentsToRemove.RemoveRange(removed);
                }
            }
        }

        public void RemoveComponentAndRequiredComponents(Component component, List<Component> removed)
        {
            //TODO check for cicular dependancies

            removed.Add(component);
            
            List<Component> requiredByComponents = component.RequiredByComponents(component.gameObject.GetComponents<Component>().ToList());

            foreach (Component requiredByComponent in requiredByComponents)
            {
                RemoveComponentAndRequiredComponents(requiredByComponent, removed);
            }

            RemoveComponent(component);
        }

        private void RemoveGameObject(GameObject gameObject)
        {
            if (Application.isPlaying)
            {
                Destroy(gameObject);
            }
            else
            {
                #if UNITY_EDITOR
                try {
                    if (gameObject.IsPrefab())
                    {
                        Debug.Log("If you get a warning about 'Setting the parent of a transform..'" +
                            " it can be ignored.");
                    }

                    Undo.DestroyObjectImmediate(gameObject); //Thows unity warning but works
                    
                    //DestroyImmediate(gameObject, true); //Can't undo
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(exception);
                }
                #endif
            }
        }

        private void RemoveComponent(Component component)
        {
            if (Application.isPlaying)
            {
                Destroy(component);
            }
            else
            {
                #if UNITY_EDITOR
                Undo.DestroyObjectImmediate(component);
                #endif
            }
        }

        public void CopyComponentsAndChildren(GameObject from, GameObject to, bool ignoreTransform=true, bool ignorePrefabLink=true)
        {
            if (from != null)
            {
                List<Component> componentsToAdd = from.GetComponents<Component>().ToList();

                //Copy prefab components
                while (componentsToAdd.Count > 0)
                {
                    CopyComponentAndRequiredComponents(componentsToAdd[0], componentsToAdd, to, ignoreTransform, ignorePrefabLink);
                }

                //Copy prefab children
                foreach (Transform child in from.transform)
                {
                    Transform newChild = Instantiate(child, to.transform, false);
                    newChild.name = child.name;

                    #if UNITY_EDITOR
                    Undo.RegisterCreatedObjectUndo(newChild.gameObject, "Prefab Link: Copy child GameObject");
                    #endif
                }

                if (ExtensionMethods.ExtensionMethods.includeNames)
                {
                    to.name = from.name;
                }
            }
        }

        public void CopyComponentAndRequiredComponents(Component component, List<Component> componentsToAdd, GameObject to, bool ignoreTransform=true, bool ignorePrefabLink=true)
        {
            componentsToAdd.Remove(component);

            bool ignore = false;

            //Not using ignoreTransform and always ignoring no matter what. 
            if (/*ignoreTransform &&*/ component.GetType() == typeof(Transform) || 
                ignorePrefabLink && component.GetType() == typeof(PrefabLink))
            {
                ignore = true;
            }

            if (ignorePrefabLink && component.GetType() == typeof(PrefabLink))
            {
                ignore = true;
            }

            if (!ignore)
            {
                List<Type> requireComponentTypes = component.RequiredComponents();

                foreach (Type type in requireComponentTypes)
                {
                    if (to.GetComponent(type) == null)
                    {
                        Component requiredComponent = componentsToAdd.Find((item) => item.GetType() == type);
                        CopyComponentAndRequiredComponents(requiredComponent, componentsToAdd, to, ignoreTransform);
                    }
                }

                CopyComponent(component, to);
            }
        }

        private Component CopyComponent(Component component, GameObject to)
        {
            if (to == null)
            {
                to = gameObject;
            }

            return to.CopyComponent(component);
        }

        private List<PrefabLink> DirectChildPrefabLinks(GameObject gameObject)
        {
            List<PrefabLink> directChildPrefabLinks = new List<PrefabLink> { };

            foreach (Transform child in gameObject.transform)
            {
                PrefabLink directChildPrefabLink = child.gameObject.GetComponent<PrefabLink>();
                bool isPrefabLink = directChildPrefabLink != null;

                if (isPrefabLink)
                {
                    directChildPrefabLinks.Add(directChildPrefabLink);
                }
                else
                {
                    directChildPrefabLinks.AddRange(DirectChildPrefabLinks(child.gameObject));
                }
            }

            return directChildPrefabLinks;
        }

        public float StartTime
        {
            get
            {
                return revertStartTime;
            }

            set
            {
                revertStartTime = value;
            }
        }

        public bool copySuccessful
        {
            get
            {
                return revertSuccessful;
            }

            set
            {
                revertSuccessful = value;
            }
        }

        public bool Dirty
        {
            get
            {
                return dirty;
            }

            set
            {
                dirty = value;
            }
        }

        public GameObject Target
        {
            get
            {
                return target;
            }

            set
            {
                target = value;
            }
        }
    }
}

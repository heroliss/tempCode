using System;
using Ddz.Define;
using UnityEngine;

namespace Ddz.BoloExtensions.Component
{
    public class LocalizationBase : MonoBehaviour
    {
        private void Awake()
        {
            RefreshView();
            GameGlobal.EventMgr.On(eEventCommon.EVENT_LANGUAGE_CHANGE, OnLanguageChange);
        }

        private void OnDestroy()
        {
            GameGlobal.EventMgr.Off(eEventCommon.EVENT_LANGUAGE_CHANGE, OnLanguageChange);
        }

        private void OnLanguageChange(Enum e, object arg)
        {
            this.RefreshView();
        }

        protected virtual void RefreshView()
        {
            
        }
    }
}

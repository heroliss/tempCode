using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityGameFramework.Runtime;

public class ClickOutsideTrigger : MonoBehaviour
{
    [SerializeField] List<GameObject> innerItems = new List<GameObject>();
    List<GameObject> inners = new List<GameObject>();

    private Action clickOutsideEvent;

    private void Awake() {
        inners.Clear();
        Image tempImg;
        RawImage tempRawImg;
        //获取根节点下有射线检测的对象
        foreach (Transform childTran in transform) {
            tempImg = childTran.GetComponent<Image>();
            if (tempImg != null) {
                if (tempImg.raycastTarget) {
                    inners.Add(childTran.gameObject);
                    continue;
                }
            }
            tempRawImg = childTran.GetComponent<RawImage>();
            if (tempRawImg != null) {
                if (tempRawImg.raycastTarget) {
                    inners.Add(childTran.gameObject);
                    continue;
                }
            }
        }
        inners.AddRange(innerItems);
    }

    // Update is called once per frame
    void Update() {
        // 检测鼠标左键点击
        if (Input.GetMouseButtonDown(0)) {
            // 获取当前指针位置的所有UI命中结果
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = Input.mousePosition;
            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            bool isInner = false;
            foreach (GameObject go in inners) {
                foreach (RaycastResult result in results) {
                    if (go == result.gameObject) {
                        isInner = true;
                        break;
                    }
                }
            }

            //EventSystem.current.IsPointerOverGameObject()
            // 检查是否点击在UI元素上
            BoloLog.LogInfo(isInner ?
                "点击在UI:["+ gameObject.name +"]上" : "点击在UI:["+ gameObject.name +"]外");

            if (!isInner) {
                // 如果点击不在UI上，关闭当前UI
                if (clickOutsideEvent != null) {
                    //按事件处理
                    clickOutsideEvent.Invoke();
                }
                else {
                    //默认关闭
                    gameObject.SetActive(false);
                }  
            }
        }
    }

    public void AddClickOutsideEvent(Action _event) {
        if (_event != null) {
            clickOutsideEvent += _event;
        }
    }

    private void OnDestroy() {
        inners.Clear();
        clickOutsideEvent = null;
    }
}

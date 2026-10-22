using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace GameCore
{
    /// <summary>
    /// 单张卡牌的 UI 视图。挂在手牌里的卡牌预制体根节点上。
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【层级结构】
    /// ══════════════════════════════════════════════════════════════
    ///   ┌──────────────────────────────┐
    ///   │ ① frameImage     卡框        │ ← 按 (风格 × 稀有度) 自动查表
    ///   │   ┌────────────────────────┐ │
    ///   │   │ ② backgroundImage      │ │ ← 主插画（来自 CardSkin）
    ///   │   │    卡牌背景 / 主插画    │ │
    ///   │   └────────────────────────┘ │
    ///   │ ③ descriptionText   描述     │
    ///   │ ④ staminaCostText   体力费用 │
    ///   │ ⑤ energyCostText    能量费用 │
    ///   └──────────────────────────────┘
    ///
    /// ══════════════════════════════════════════════════════════════
    ///  【职责边界】
    /// ══════════════════════════════════════════════════════════════
    ///   ✅ 读 CardDataSO + CardInstance，把数据画到 UI 上
    ///   ❌ 不持有战斗逻辑
    ///   ❌ 不修改任何数据（SO 只读）
    /// ══════════════════════════════════════════════════════════════
    /// </summary>
    public class CardView : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  三层图像
        // ══════════════════════════════════════════════════════════
        [Header("① 卡框（由 CardFrameTableSO 自动查）")]
        [Tooltip("对应场景里的 cardframe")]
        public Image frameImage;

        [Header("② 卡牌背景 / 主插画")]
        [Tooltip("对应场景里的 cardart")]
        public Image backgroundImage;

        // ══════════════════════════════════════════════════════════
        //  文本
        // ══════════════════════════════════════════════════════════
        [Header("③ 文本")]
        [Tooltip("对应 cardtext")]
        public TMP_Text descriptionText;

        [Tooltip("对应 cardcostphysical（体力费用）")]
        public TMP_Text staminaCostText;

        [Tooltip("对应 cardcostenergy（能量费用）")]
        public TMP_Text energyCostText;

        // ══════════════════════════════════════════════════════════
        //  卡框表
        // ══════════════════════════════════════════════════════════
        [Header("④ 卡框表（全局唯一的那个资产）")]
        public CardFrameTableSO frameTable;

        // ══════════════════════════════════════════════════════════
        //  数据
        // ══════════════════════════════════════════════════════════
        [Header("当前卡牌（运行时由 Bind 设置；编辑器里可手动拖来预览）")]
        public CardDataSO cardData;

        /// <summary>运行时实例（等级 + 升级进度）。编辑器预览时为 null</summary>
        private CardInstance _inst;

        /// <summary>编辑器预览用的临时实例（避免每次 Refresh 都 new）</summary>
        private CardInstance _previewInst;

        // ══════════════════════════════════════════════════════════
        //  外部入口
        // ══════════════════════════════════════════════════════════

        /// <summary>绑定卡牌数据 + 实例，并刷新显示（运行时唯一入口）</summary>
        public void Bind(CardDataSO data, CardInstance inst)
        {
            cardData = data;
            _inst = inst;
            Refresh();
        }

        /// <summary>清空显示（卡槽为空 / 回收预制体时调用）</summary>
        public void Clear()
        {
            if (frameImage != null)
            {
                frameImage.sprite = null;
                frameImage.color = new Color(1f, 1f, 1f, 0f);   // 完全透明
            }
            if (backgroundImage != null) backgroundImage.sprite = null;
            if (descriptionText != null) descriptionText.text = string.Empty;
            if (staminaCostText != null) staminaCostText.text = string.Empty;
            if (energyCostText != null) energyCostText.text = string.Empty;
        }

        // ══════════════════════════════════════════════════════════
        //  内部
        // ══════════════════════════════════════════════════════════

        /// <summary>取实际用于计算的实例（编辑器预览时造一个临时实例）</summary>
        private CardInstance GetEffectiveInstance()
        {
            if (_inst != null) return _inst;
            if (cardData == null) return null;

            // 编辑器预览：按资产的默认 level 临时造一个（升级项全 0）
            if (_previewInst == null || _previewInst.cardId != cardData.cardId)
                _previewInst = new CardInstance(cardData.cardId, cardData.upgradeEntries.Length);

            _previewInst.level = cardData.level;
            return _previewInst;
        }

        private void Refresh()
        {
            if (cardData == null)
            {
                Clear();
                return;
            }

            CardInstance inst = GetEffectiveInstance();

            // ── ① 卡框：由 (风格 × 稀有度) 自动查 ──
            if (frameImage != null)
            {
                if (frameTable != null)
                {
                    frameImage.sprite = frameTable.GetFrame(cardData.GetFrameStyle(), cardData.rarity);
                    frameImage.color = Color.white;
                }
                else
                {
                    frameImage.sprite = null;
                }
            }

            // ── ② 卡牌背景 ──
            CardSkin skin = cardData.cardSkin;
            if (backgroundImage != null)
                backgroundImage.sprite = skin != null ? skin.backgroundSprite : null;

            // ── ③ 文本 ──
            // 描述来自美术数据；费用来自战斗数据（唯一来源）
            if (descriptionText != null)
                descriptionText.text = skin != null ? skin.displayText : string.Empty;

            if (staminaCostText != null)
                staminaCostText.text = cardData.GetStaminaCost(inst).ToString();

            if (energyCostText != null)
                energyCostText.text = cardData.GetEnergyCost(inst).ToString();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 编辑器里改数据即时预览（打包时被剔除）。
        /// </summary>
        private void OnValidate()
        {
            if (Application.isPlaying) return;
            Refresh();
        }
#endif
    }
}
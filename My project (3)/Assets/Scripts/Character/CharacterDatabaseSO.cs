using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore
{
    /// <summary>
    /// 角色数据库。一个资产装下项目里所有 CharacterDataSO，按 ID 查表。
    ///
    /// 存放位置：Assets/ScriptableObjects/Characters/CharacterDatabase.asset
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterDatabase", menuName = "GameCore/Character/CharacterDatabase")]
    public class CharacterDatabaseSO : ScriptableObject
    {
        [Header("所有角色（把项目里每个 CharacterDataSO 都放进来）")]
        public CharacterDataSO[] allCharacters = new CharacterDataSO[0];

        /// <summary>ID → 角色 的索引，首次查询时构建</summary>
        private Dictionary<string, CharacterDataSO> _lookup;

        /// <summary>按 ID 查角色。找不到返回 null</summary>
        public CharacterDataSO FindById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            if (allCharacters == null) return null;

            if (_lookup == null)
            {
                _lookup = new Dictionary<string, CharacterDataSO>(allCharacters.Length);

                for (int i = 0; i < allCharacters.Length; i++)
                {
                    CharacterDataSO c = allCharacters[i];
                    if (c == null || string.IsNullOrEmpty(c.characterId)) continue;
                    _lookup[c.characterId] = c;
                }
            }

            _lookup.TryGetValue(characterId, out CharacterDataSO result);
            return result;
        }

        /// <summary>运行时改了列表后手动重建索引（一般不需要）</summary>
        public void InvalidateCache() => _lookup = null;

#if UNITY_EDITOR
        private void OnValidate()
        {
            InvalidateCache();       // 编辑器改了列表 → 下次查询重建

            if (allCharacters == null) return;

            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < allCharacters.Length; i++)
            {
                CharacterDataSO c = allCharacters[i];
                if (c == null) continue;

                if (string.IsNullOrEmpty(c.characterId))
                {
                    Debug.LogError($"[CharacterDatabase] 第 {i} 个角色没有填 characterId（{c.name}）", this);
                }
                else if (!seen.Add(c.characterId))
                {
                    Debug.LogError($"[CharacterDatabase] characterId 重复：{c.characterId}", this);
                }
            }
        }
#endif
    }
}
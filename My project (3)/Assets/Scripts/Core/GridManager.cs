using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace GameCore
{


    public class GridManager : MonoBehaviour
    {
        public static GridManager Instance { get; private set; }
        [Header("网格尺寸")]
        public Vector2Int gridSize = new Vector2Int(8, 8);
        private  readonly HashSet<Vector2Int> _walls = new HashSet<Vector2Int>();
        private readonly Dictionary<Vector2Int, UnitBase> _units = new Dictionary<Vector2Int, UnitBase>();
        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject);return; }
        }
        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
        public bool IsWall(Vector2Int grid) => _walls.Contains(grid);
        public UnitBase GetUnitAt(Vector2Int  grid)
        {
            _units.TryGetValue(grid, out UnitBase unit);
            return unit;
        }
        public void SetWall(Vector2Int grid,bool isWall)
        {
            if (isWall) _walls.Add(grid);
            else _walls.Remove(grid);

        }
        public void RegisterUnit(UnitBase unit )
        {
            if (unit == null) return;
            _units[unit.gridPos] = unit;
        }
        public void UnregisterUnit(UnitBase unit )
        {
            if (unit == null) return;
            if (_units.TryGetValue(unit.gridPos, out UnitBase u) && u == unit)
                _units.Remove(unit.gridPos);
        }
    }
}
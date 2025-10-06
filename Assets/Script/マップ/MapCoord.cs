using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct MapCoord
{
    public int x;
    public int y;

    // 生成
    public MapCoord(int x, int y)
    {
        this.x = x;
        this.y = y;
    }

    // 文字列変換 (例: "0,1")
    public override string ToString() => $"{x},{y}";

    // 文字列から変換
    public static MapCoord FromString(string key)
    {
        var parts = key.Split(',');
        return new MapCoord(int.Parse(parts[0]), int.Parse(parts[1]));
    }

    // 隣接する座標のリスト（上下左右）
    public List<MapCoord> GetNeighbors()
    {
        return new List<MapCoord>
        {
            new MapCoord(x + 1, y),
            new MapCoord(x - 1, y),
            new MapCoord(x, y + 1),
            new MapCoord(x, y - 1)
        };
    }

    // Dictionaryのキーとして使うために Equals/GetHashCode を実装
    public override bool Equals(object obj)
    {
        return obj is MapCoord other && x == other.x && y == other.y;
    }

    public override int GetHashCode()
    {
        return x * 1000 + y; // 1000マス以下のマップなら衝突しない安全な方法
    }
}


using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MusicMap.Events
{
    public static class EventMapPlacement
    {
        // Initial placement policy only; no new restriction is added to MapCell.
        public static void Generate(EventManager manager, MapDataSO map, int blockSize,
            System.Random random, IEventWorldState world, IEnumerable<Vector2Int> reservedCells = null)
        {
            if (manager == null || map == null || map.width <= 0 || map.height <= 0 || blockSize <= 0 || random == null)
            { Debug.LogError("[EventMapPlacement] Invalid manager, map, block size, or random source."); return; }
            map.EnsureGrid();
            var used = new HashSet<Vector2Int>(reservedCells ?? Enumerable.Empty<Vector2Int>());
            var allCells = new List<Vector2Int>();
            for (int y = 0; y < map.height; y++)
                for (int x = 0; x < map.width; x++)
                {
                    var position = new Vector2Int(x, y);
                    var cell = map.mapGrid[x, y];
                    if (!string.IsNullOrEmpty(cell.eventInstanceId) || cell.EvData != null) used.Add(position);
                    if (!used.Contains(position)) allCells.Add(position);
                }

            var context = new EventContext { world = world };
            var candidates = manager.Catalog.GetEnabled().Where(d => d.appearance != null && d.appearance.generateOnMap
                && (!d.appearance.requiresUnlock || manager.IsUnlocked(d.PersistentId))
                && manager.CanAppear(d) && context.Matches(d.appearance.conditions)).ToArray();

            // Match required slots before spawning. Reassign planned cells if a broad-condition
            // event would consume the only cell eligible for another required event.
            var requests = new List<(EventDefinitionSO definition, List<Vector2Int> cells)>();
            foreach (var definition in candidates.Where(d => d.appearance.requiredPlacements > 0))
            {
                var cells = allCells.Where(p => MatchesBackground(definition, map.mapGrid[p.x, p.y])).ToList();
                Shuffle(cells, random);
                int count = Math.Min(definition.appearance.requiredPlacements, allCells.Count);
                for (int i = 0; i < count; i++) requests.Add((definition, cells));
            }
            var owners = new Dictionary<Vector2Int, int>();
            for (int i = 0; i < requests.Count; i++) Assign(i, new HashSet<Vector2Int>());

            bool Assign(int request, HashSet<Vector2Int> visited)
            {
                foreach (var position in requests[request].cells)
                {
                    if (!visited.Add(position)) continue;
                    if (!owners.TryGetValue(position, out int previous) || Assign(previous, visited))
                    { owners[position] = request; return true; }
                }
                return false;
            }

            var placed = new Dictionary<EventDefinitionSO, int>();
            foreach (var entry in owners.OrderBy(e => e.Value))
            {
                var definition = requests[entry.Value].definition;
                if (manager.Spawn(definition, entry.Key) == null) continue;
                used.Add(entry.Key);
                placed.TryGetValue(definition, out int count);
                placed[definition] = count + 1;
            }
            foreach (var definition in candidates.Where(d => d.appearance.requiredPlacements > 0))
            {
                placed.TryGetValue(definition, out int count);
                if (count < definition.appearance.requiredPlacements)
                    Debug.LogError($"[EventMapPlacement] Required event '{definition.eventCode}' could not be placed: {count}/{definition.appearance.requiredPlacements}. No eligible unoccupied cells or Spawn failed.");
            }

            var randomDefinitions = candidates.Where(d => d.appearance.requiredPlacements <= 0
                && d.appearance.weight > 0 && !float.IsInfinity(d.appearance.weight) && !float.IsNaN(d.appearance.weight)).ToArray();
            var blocks = new List<Vector2Int>();
            for (int y = 0; y < map.height; y += blockSize)
                for (int x = 0; x < map.width; x += blockSize) blocks.Add(new Vector2Int(x, y));
            Shuffle(blocks, random);
            foreach (var block in blocks)
            {
                var cells = new List<Vector2Int>();
                for (int y = block.y; y < Math.Min(block.y + blockSize, map.height); y++)
                    for (int x = block.x; x < Math.Min(block.x + blockSize, map.width); x++)
                    {
                        var position = new Vector2Int(x, y);
                        var cell = map.mapGrid[x, y];
                        if (!used.Contains(position) && string.IsNullOrEmpty(cell.eventInstanceId) && cell.EvData == null)
                            cells.Add(position);
                    }
                var pool = randomDefinitions.Select(d => (definition: d,
                    cells: cells.Where(p => MatchesBackground(d, map.mapGrid[p.x, p.y])).ToArray()))
                    .Where(candidate => candidate.cells.Length > 0).ToArray();
                if (pool.Length == 0)
                {
                    Debug.LogWarning($"[EventMapPlacement] Skipped random block ({block.x},{block.y}): no eligible event/cell.");
                    continue;
                }
                double selection = random.NextDouble() * pool.Sum(candidate => (double)candidate.definition.appearance.weight);
                var selected = pool[pool.Length - 1];
                foreach (var candidate in pool)
                {
                    selection -= candidate.definition.appearance.weight;
                    if (selection < 0) { selected = candidate; break; }
                }
                var target = selected.cells[random.Next(selected.cells.Length)];
                if (manager.Spawn(selected.definition, target) != null) used.Add(target);
                else Debug.LogWarning($"[EventMapPlacement] Skipped random block ({block.x},{block.y}): Spawn failed.");
            }
        }

        private static bool MatchesBackground(EventDefinitionSO definition, MapCell cell) => definition.appearance.backgroundCategories == null
            || definition.appearance.backgroundCategories.Count == 0 || definition.appearance.backgroundCategories.Contains(cell.bgCategory?.bgCategoryName);

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }
    }
}

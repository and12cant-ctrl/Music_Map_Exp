using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MapGenerator : MonoBehaviour
{
    private readonly List<MinimapCellView> _fullCells = new();
    private readonly List<MinimapCellView> _miniCells = new();
    private const int ViewCells = 9;
    int WidthCount;
    int HeightCount;
    int totalBlocks;
    //int MapGrid = 5;
    BackgroundCategory BG;  //これにBackGroundListSOからランダムにcategoryを入れる
    EventData EV;
    CreateEvent cre;
    System.Random rand = new System.Random();
    public void MapGenerate(MapDataSO questMap, BGCategoryListSO requiredBgList,
        BGCategoryListSO randamBgList, EventListSO requiredEvList, EventListSO randamEvList, int MapGridInt)
    {
        if (questMap == null || questMap.width <= 0 || questMap.height <= 0 || MapGridInt <= 0 ||
            requiredBgList?.bGCategoryList == null || randamBgList?.bGCategoryList == null)
        { Debug.LogError("[MapGenerator] Invalid map, block size, or background lists."); return; }

        int blocksX = (questMap.width - 1) / MapGridInt + 1;
        int blocksY = (questMap.height - 1) / MapGridInt + 1;
        var backgrounds = requiredBgList.bGCategoryList.Where(b => b != null).ToList();
        var randomBackgrounds = randamBgList.bGCategoryList.Where(b => b != null).ToArray();
        if (backgrounds.Count < blocksX * blocksY && randomBackgrounds.Length == 0)
        { Debug.LogError("[MapGenerator] Random background pool is empty."); return; }
        while (backgrounds.Count < blocksX * blocksY)
            backgrounds.Add(randomBackgrounds[rand.Next(randomBackgrounds.Length)]);
        backgrounds = backgrounds.OrderBy(_ => rand.Next()).ToList();

        var builder = GetComponent<MiniMapBuilder>();
        bool rebuildView = builder != null && builder.IsBuilt;
        var eventSystem = GetComponent<MusicMap.Events.EventSystemRoot>();
        // There is deliberately no fallback to legacy event generation.
        if (eventSystem == null || !eventSystem.PrepareNewMap(questMap))
        { Debug.LogError("[MapGenerator] Connect EventSystemRoot and its catalog before generating a new map."); return; }

        questMap.EnsureGrid();
        // Background assignment does not depend on the Odin selection rectangle.
        for (int y = 0; y < questMap.height; y++)
            for (int x = 0; x < questMap.width; x++)
            {
                var background = backgrounds[(y / MapGridInt) * blocksX + x / MapGridInt];
                questMap.mapGrid[x, y].bgCategory = background;
                questMap.mapGrid[x, y].backgroundId = background.bgCategoryName;
            }

        var game = GetComponent<GameManager>();
        var reserved = eventSystem.reserveBaseCell && game != null
            ? new[] { game.BaseCell } : new Vector2Int[0];
        MusicMap.Events.EventMapPlacement.Generate(eventSystem.Manager, questMap, MapGridInt, rand,
            eventSystem.World, reserved);
        if (rebuildView) builder.SetupMap();
        foreach (var tab in GetComponentsInChildren<HakenTab>(true)) tab.RefreshCurrentCell();
    }

    // Retained for migration reference only. The runtime entry point never calls this method.
    private void GenerateLegacyMap(
        MapDataSO questMap,
        BGCategoryListSO requiredBgList,
        BGCategoryListSO randamBgList,
        EventListSO requiredEvList,
        EventListSO randamEvList,
        int MapGridInt
        )
    {
        #region 変数生成
        if (questMap == null || MapGridInt <= 0 || requiredBgList == null || randamBgList == null)
        { Debug.LogError("[MapGenerator] Invalid map, block size, or background lists."); return; }
        questMap.EnsureGrid();
        WidthCount = questMap.width / MapGridInt;
        HeightCount = questMap.height / MapGridInt;
        totalBlocks = WidthCount * HeightCount;
        #endregion

        #region 背景ランダム挿入
        List<BackgroundCategory> assignedBGs = requiredBgList.bGCategoryList.ToList();

        List<BackgroundCategory> randomBGPool = randamBgList.bGCategoryList;
        if (assignedBGs.Count < totalBlocks && randomBGPool.Count == 0)
        { Debug.LogError("[MapGenerator] Random background pool is empty."); return; }
        while (assignedBGs.Count < totalBlocks)
        {
            var randomCat = randomBGPool[rand.Next(randomBGPool.Count)];
            assignedBGs.Add(randomCat);
        }

        assignedBGs = assignedBGs.OrderBy(x => rand.Next()).ToList();
        #endregion

        #region イベントランダム挿入
        //List<string> assignedEVs = requiredEvList.evCategoryList.Select(dog => dog.evCategoryName).ToList();

        cre = new CreateEvent();
        List<EventData> assigenList = new List<EventData>();
        List<EventData> MainEv = new List<EventData>();
        CategoryNamer naming = new CategoryNamer();

        #region イベントカテゴリ内の物を削除
        foreach (var cat in requiredEvList.evCategoryList)
        {
            cat.evDatas.Clear();
        }
        foreach (var cat in randamEvList.evCategoryList)
        {
            cat.evDatas.Clear();
        }
        #endregion

        foreach (var cat in requiredEvList.evCategoryList)
        {
            string nameId = naming.GetNextNameID(cat.evCategoryName);
            MainEv.Add(cre.CreateEv(cat,nameId));
        }

        var randomEVPool = randamEvList.evCategoryList;

        while (assigenList.Count < totalBlocks)
        {
            var randomdog = randomEVPool[rand.Next(randomEVPool.Count)];
            string nameId = naming.GetNextNameID(randomdog.evCategoryName);
            assigenList.Add(cre.CreateEv(randomdog, nameId));
        }

        assigenList = assigenList.OrderBy(x => rand.Next()).ToList();
        #endregion

        int index = 0;
        for (int i = 0; i < HeightCount; i++)
        {
            for (int j = 0; j < WidthCount; j++)
            {
                Vector2Int Spos = new Vector2Int(i * MapGridInt, j * MapGridInt);
                Vector2Int Epos = new Vector2Int((i + 1) * MapGridInt - 1, (j + 1) * MapGridInt - 1);
                BG = assignedBGs[index];
                EV = assigenList[index];

                index++;
                EventRandom(i * MapGridInt, j * MapGridInt, MapGridInt, MapGridInt, EV,questMap);
                questMap.ApplyBackground(Spos, Epos, BG);
            }
        }

        foreach (var assigen in MainEv)
        {
            int XX = MapGridInt * rand.Next(0, HeightCount);
            int YY = MapGridInt * rand.Next(0, WidthCount);
            EventRandom(XX,YY,MapGridInt,MapGridInt,assigen,questMap);
        }
    }

    public void EventRandom(int X, int Y, int Width, int Height, EventData evCat, MapDataSO questMap)
    {
        int faiX = 0, faiY = 0;
        int i = 0;
        while (questMap.mapGrid[faiX, faiY].EvData != null)
        {
            i++;
            if (i == 20)
            {
                for (int j = 0; j < Width; j++)
                {
                    for (int k = 0; k < Height; k++)
                    {
                        if (questMap.mapGrid[X + j, Y + k].EvData != null)
                        {
                            questMap.ApplyEvnet(X + j, Y + k, evCat);
                            return;
                        }
                    }
                }
            }
            RandIntSelect();
        }
        //Debug.Log($"[{faiX}][{faiY}]{evCat.id}");
        questMap.ApplyEvnet(faiX, faiY, evCat);
        void RandIntSelect()
        {
            int wid = rand.Next(0, Width), hei = rand.Next(0, Height);
            faiX = X + wid;
            faiY = Y + hei;
        }
    }

    public class CreateEvent {
        System.Random r = new System.Random();
        public EventData CreateEv(EventCategory EvCate,string Id)
        {
            EventData Target = new EventData();
            EvCate.evDatas.Add(Target);
            Target.id = Id;
            Target.icon = EvCate.catIcon;

            Target.likeLevel = RandomReturn() switch 
            {
                0 or 1 or 2 => likeability.Low,
                3 or 4 => likeability.middle,
                5 => likeability.High,
                _ => likeability.Low
            };
            Target.mysteryLevel = RandomReturn() switch
            {
                0 or 1 or 2 => mystery.Low,
                3 or 4 => mystery.middle,
                5 => mystery.High,
                _ => mystery.Low
            };
            return Target;
        }
        public int RandomReturn()
        {
            return r.Next(0, 6);
        }
    }

    public class CategoryNamer
    {
        private string[] directions = { "第一", "第二", "第三", "第四","北", "西", "東", "南"};
        private Dictionary<string, int> categoryCount = new Dictionary<string, int>();

        public string GetNextNameID(string name)
        {
            if(!categoryCount.ContainsKey(name))
                categoryCount[name] = 0;
            int count = categoryCount[name];
            string prefix = directions[count %  directions.Length];
            categoryCount[name]++;
            return prefix + name;
        }
        public void Reset()
        {
            categoryCount.Clear();
        }
    }
}


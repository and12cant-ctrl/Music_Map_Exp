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
    CreateEvent cre = new CreateEvent();
    System.Random rand = new System.Random();
    public void MapGenerate(
        MapDataSO questMap,
        BGCategoryListSO requiredBgList,
        BGCategoryListSO randamBgList,
        EventListSO requiredEvList,
        EventListSO randamEvList,
        int MapGridInt
        )
    {
        #region 変数生成
        WidthCount = questMap.width / MapGridInt;
        HeightCount = questMap.height / MapGridInt;
        totalBlocks = WidthCount * HeightCount;
        #endregion

        #region 背景ランダム挿入
        List<BackgroundCategory> assignedBGs = requiredBgList.bGCategoryList.ToList();

        List<BackgroundCategory> randomBGPool = randamBgList.bGCategoryList;
        while (assignedBGs.Count < totalBlocks)
        {
            var randomCat = randomBGPool[rand.Next(randomBGPool.Count)];
            assignedBGs.Add(randomCat);
        }

        assignedBGs = assignedBGs.OrderBy(x => rand.Next()).ToList();
        #endregion

        #region イベントランダム挿入
        //List<string> assignedEVs = requiredEvList.evCategoryList.Select(dog => dog.evCategoryName).ToList();

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
            MainEv.Add(cre.CreateEv(cat,nameId));//kokomade
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


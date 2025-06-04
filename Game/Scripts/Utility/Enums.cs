using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Assets.Game.Scripts.Utility
{
    public enum KeyWords
    {
        Datasources,
        Container,
        Canvas,
    }

    public enum CA
    {
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        Center,
        CenterBottom,
    }

    public enum EItemQuality
    {       
        Poor,        
        Normal,
        High,
        Exceptional,
    }

    public enum ETask2
    {
        Move,
        Examine,
        Camp,
        Gather,
        ChopWood,
        Mine,
        Hunt,
        MakeMap,
        Craft,
        VisitTavern,
    }

    public enum ESkill
    {
        Alchemy,
        Assembling,
        Bowyery,
        Brewing,
        Butchery,
        Carpentry,
        Carving,
        Construction,
        Cooking,
        Diplomacy,
        Enchanting,
        Farming,
        Fishing,
        Forging,
        Gathering,
        Gem_cutting,
        Glass_blowing,
        Herbalism,
        Herding,
        Hunting,
        Jewellery,
        Labor,
        Literacy,
        Masonry,
        Metallurgy,
        Mining,
        Painting,
        Pottery,
        Riding,
        Sewing,
        Shoemaking,
        Treecutting,
        Tailoring,
        Tanning,
        Trade,
        Weaving,
        Saddling,

        SkillCount = Trade,
    }

    public enum EStatus
    {
        New,        
        InProgress,
        RewardPending,
        Completed,
        Failed,
        Cancelled,
    }

    public enum EBuildingFunc
    { 
        None,
        House = 1,
        Warehouse = 2,
        Workshop = 4,
        Store = 8,
        Owned = 16,
        Any = 32,
    }

    public enum EContractType
    {                
        OneTime,
        RecurringTask,
        Permanent,
    }

    public enum EstTarget
    {        
        Production,
        Supplies,
        Stock,
        Sales,
        Free_storage,
        Employees,
    }

}
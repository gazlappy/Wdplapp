namespace Wdpl2.Features.WebsiteBuilder.Logo;

/// <summary>
/// A large curated catalog of emoji/symbol icons that can be used as the central icon
/// in a logo. Grouped by category so the picker can render section headers.
/// </summary>
public static class IconCatalog
{
    public sealed record IconInfo(string Glyph, string Name, string Category);

    public static readonly IReadOnlyList<IconInfo> All = new IconInfo[]
    {
        // ---- Sport / Pool ----
        new("\U0001F3B1", "8-Ball",          "Sport"),
        new("\U0001F3AF", "Target",          "Sport"),
        new("\U0001F3C6", "Trophy",          "Sport"),
        new("\U0001F947", "Gold Medal",      "Sport"),
        new("\U0001F948", "Silver Medal",    "Sport"),
        new("\U0001F949", "Bronze Medal",    "Sport"),
        new("\U0001F3C5", "Sports Medal",    "Sport"),
        new("\U0001F3F5", "Rosette",         "Sport"),
        new("\U0001F3F8", "Badminton",       "Sport"),
        new("\U0001F3D3", "Ping Pong",       "Sport"),
        new("\U0001F3BE", "Tennis",          "Sport"),
        new("\u26BD",     "Soccer",          "Sport"),
        new("\u26BE",     "Baseball",        "Sport"),
        new("\U0001F3C0", "Basketball",      "Sport"),
        new("\U0001F3C8", "Football",        "Sport"),
        new("\U0001F3D2", "Hockey",          "Sport"),
        new("\U0001F3CC", "Golf",            "Sport"),
        new("\U0001F3BD", "Running Shirt",   "Sport"),
        new("\U0001F94A", "Boxing Glove",    "Sport"),
        new("\U0001F94B", "Martial Arts",    "Sport"),
        new("\U0001F3F9", "Bow & Arrow",     "Sport"),

        // ---- Symbols ----
        new("\u2605",     "Star",            "Symbols"),
        new("\u2606",     "Star Outline",    "Symbols"),
        new("\u2660",     "Spade",           "Symbols"),
        new("\u2663",     "Club",            "Symbols"),
        new("\u2665",     "Heart",           "Symbols"),
        new("\u2666",     "Diamond",         "Symbols"),
        new("\u2666\uFE0F","Diamond Glyph",  "Symbols"),
        new("\u2728",     "Sparkles",        "Symbols"),
        new("\u2B50",     "Glowing Star",    "Symbols"),
        new("\u26A1",     "Lightning",       "Symbols"),
        new("\U0001F525", "Fire",            "Symbols"),
        new("\U0001F4A5", "Boom",            "Symbols"),
        new("\U0001F4AF", "100",             "Symbols"),
        new("\U0001F451", "Crown",           "Symbols"),
        new("\U0001F48E", "Gem",             "Symbols"),
        new("\u2697",     "Alembic",         "Symbols"),
        new("\u269C",     "Fleur-de-Lis",    "Symbols"),
        new("\u2694",     "Crossed Swords",  "Symbols"),
        new("\u2693",     "Anchor",          "Symbols"),

        // ---- Animals ----
        new("\U0001F981", "Lion",            "Animals"),
        new("\U0001F405", "Tiger",           "Animals"),
        new("\U0001F43B", "Bear",            "Animals"),
        new("\U0001F43A", "Wolf",            "Animals"),
        new("\U0001F40D", "Snake",           "Animals"),
        new("\U0001F40E", "Horse",           "Animals"),
        new("\U0001F985", "Eagle",           "Animals"),
        new("\U0001F989", "Owl",             "Animals"),
        new("\U0001F993", "Zebra",           "Animals"),
        new("\U0001F98D", "Gorilla",         "Animals"),
        new("\U0001F996", "T-Rex",           "Animals"),
        new("\U0001F409", "Dragon",          "Animals"),
        new("\U0001F988", "Shark",           "Animals"),
        new("\U0001F419", "Octopus",         "Animals"),
        new("\U0001F41D", "Bee",             "Animals"),
        new("\U0001F40D", "Cobra",           "Animals"),

        // ---- Nature ----
        new("\u2600\uFE0F","Sun",            "Nature"),
        new("\U0001F319", "Moon",            "Nature"),
        new("\u2728",     "Stars",           "Nature"),
        new("\U0001F30A", "Wave",            "Nature"),
        new("\U0001F30B", "Volcano",         "Nature"),
        new("\U0001F33F", "Leaf",            "Nature"),
        new("\U0001F33A", "Flower",          "Nature"),
        new("\U0001F332", "Tree",            "Nature"),
        new("\u26F0",     "Mountain",        "Nature"),
        new("\U0001F308", "Rainbow",         "Nature"),
        new("\u2744\uFE0F","Snowflake",      "Nature"),

        // ---- Tech ----
        new("\U0001F4BB", "Laptop",          "Tech"),
        new("\U0001F4F1", "Phone",           "Tech"),
        new("\U0001F50C", "Plug",            "Tech"),
        new("\U0001F50B", "Battery",         "Tech"),
        new("\U0001F6F0", "Satellite",       "Tech"),
        new("\U0001F680", "Rocket",          "Tech"),
        new("\U0001F6E0", "Tools",           "Tech"),
        new("\u2699\uFE0F","Gear",           "Tech"),
        new("\U0001F4BE", "Disk",            "Tech"),

        // ---- Food / Drink ----
        new("\U0001F37A", "Beer",            "Food"),
        new("\U0001F37B", "Beers",           "Food"),
        new("\U0001F377", "Wine",            "Food"),
        new("\U0001F942", "Champagne",       "Food"),
        new("\U0001F943", "Tumbler",         "Food"),
        new("\U0001F378", "Cocktail",        "Food"),
        new("\U0001F379", "Tropical Drink",  "Food"),
        new("\u2615",     "Coffee",          "Food"),
        new("\U0001F355", "Pizza",           "Food"),
        new("\U0001F354", "Burger",          "Food"),

        // ---- Letters / Numbers (decorative) ----
        new("\u24B6", "A Circled", "Letters"),
        new("\u24B7", "B Circled", "Letters"),
        new("\u24B8", "C Circled", "Letters"),
        new("\u24B9", "D Circled", "Letters"),
        new("\u24BA", "E Circled", "Letters"),
        new("\u24BB", "F Circled", "Letters"),
        new("\u24BC", "G Circled", "Letters"),
        new("\u24BD", "H Circled", "Letters"),
        new("\u24BE", "I Circled", "Letters"),
        new("\u24BF", "J Circled", "Letters"),
        new("\u24C0", "K Circled", "Letters"),
        new("\u24C1", "L Circled", "Letters"),
        new("\u24C2", "M Circled", "Letters"),
        new("\u24C3", "N Circled", "Letters"),
        new("\u24C4", "O Circled", "Letters"),
        new("\u24C5", "P Circled", "Letters"),
        new("\u24C6", "Q Circled", "Letters"),
        new("\u24C7", "R Circled", "Letters"),
        new("\u24C8", "S Circled", "Letters"),
        new("\u24C9", "T Circled", "Letters"),
        new("\u24CA", "U Circled", "Letters"),
        new("\u24CB", "V Circled", "Letters"),
        new("\u24CC", "W Circled", "Letters"),
        new("\u24CD", "X Circled", "Letters"),
        new("\u24CE", "Y Circled", "Letters"),
        new("\u24CF", "Z Circled", "Letters"),

        // ---- Misc ----
        new("\U0001F3B5", "Music Note",      "Misc"),
        new("\U0001F3AD", "Theatre",         "Misc"),
        new("\U0001F3A8", "Art",             "Misc"),
        new("\U0001F3AC", "Clapper",         "Misc"),
        new("\U0001F3A4", "Microphone",      "Misc"),
        new("\U0001F4F7", "Camera",          "Misc"),
        new("\U0001F3AE", "Game Controller", "Misc"),
        new("\U0001F3B2", "Dice",            "Misc"),
        new("\U0001F3B0", "Slot Machine",    "Misc"),
        new("\U0001F3F4", "Black Flag",      "Misc"),
        new("\U0001F3C1", "Chequered Flag",  "Misc"),
        new("\U0001F698", "Car",             "Misc"),
        new("\u2708\uFE0F","Plane",          "Misc"),
        new("\u2693",     "Anchor 2",        "Misc"),
    };

    public static IEnumerable<string> Categories =>
        All.Select(i => i.Category).Distinct();
}

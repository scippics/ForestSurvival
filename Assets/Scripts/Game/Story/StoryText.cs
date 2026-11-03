using System.Collections.Generic;

// Весь текст пролога в одном месте.
public static class StoryText
{
    public struct Topic
    {
        public string Question;
        public string Answer;

        public Topic(string question, string answer)
        {
            Question = question;
            Answer = answer;
        }
    }

    public const string NpcName = "Кузьмич";
    public const string NpcLeave = "Ладно, пойду.";
    public const string NpcBye = "Иди. Дотемна управься.";

    public static string NpcGreeting(StoryStage stage)
    {
        switch (stage)
        {
            case StoryStage.PourFuel: return "Нашёл, значит. Лей, не стой.";
            case StoryStage.DriveNorth:
            case StoryStage.Free: return "Ты ещё тут? Светло пока, ехал бы.";
            default: return "Гляди-ка, живой человек. На машине приехал, а заглох у самых ворот. Я слышал.";
        }
    }

    public static void NpcTopics(StoryStage stage, List<Topic> topics)
    {
        topics.Clear();
        topics.Add(new Topic("Ты кто такой?",
            "Кузьмич я. Лес валил на сороковом, пока участок не закрыли. К Савельеву за солью пришёл, третий день жду."));

        if (stage <= StoryStage.FindFuel)
            topics.Add(new Topic("Где Савельев?",
                "В доме его нет. Лампа горит, а на столе бумага лежит. Я читать не стал, не мне писано. Сходи глянь."));
        else if (stage < StoryStage.Free)
            topics.Add(new Topic("Савельев на стоянку ушёл?",
                "Раз написал, значит, ушёл. Только пса с собой не взял, а пёс за ним везде ходил. Теперь и пса нет."));
        else
            topics.Add(new Topic("Савельев ушёл к реке.",
                "К реке, значит. Сколько тут живу, с реки ещё никто не возвращался той же дорогой."));

        if (stage < StoryStage.PourFuel)
            topics.Add(new Topic("Бензин тут есть?",
                "На кордоне сухо. Брошенный седан видел, южнее по просеке? Шланг и канистра у крыльца. Шланг в бак, конец к канистре и тяни. Отпусти вовремя, а то нахлебаешься."));
        else if (stage == StoryStage.PourFuel)
            topics.Add(new Topic("Куда лить?",
                "Неси к машине, смотри на неё и держи E. Канистра тяжёлая, так что не беги."));
        else
            topics.Add(new Topic("Куда теперь ехать?",
                "На север, по просеке. Там старая стоянка лесорубов, наша. Заночевать там можно, если совсем прижмёт."));

        topics.Add(new Topic("Откуда тут столько брошенных машин?",
            "Кто доехал, кто нет. Их по всему лесу полно. Ключ у тебя есть: зажми G и откручивай, что осталось. Своей машине пригодится, только вес помни."));

        topics.Add(new Topic("Что тут ночью ходит?",
            "Стоит в тумане и смотрит. Подойдёшь, а его нет. Я потому и в дом не захожу: из окна двора не видно. Лучше тут, у дров."));

        if (stage >= StoryStage.DriveNorth)
            topics.Add(new Topic("Поехали со мной.",
                "Нет, я своё отъездил. Кто-то должен тут быть, если Савельев вернётся."));
    }

    public static readonly string[] IntroCards =
    {
        "Октябрь. Северный лесхоз, квадрат 41.",
        "Кордон №7 не выходит на связь девятый день.",
        "Лесника зовут Савельев. Двадцать лет на участке, ни одного пропущенного сеанса.",
        "Послали тебя: одна машина, один техник и полбака на всю дорогу.",
        "Полбака оказалось меньше, чем обещал завгар."
    };

    public const string Title = "КОРДОН №7";
    public const string TitleLine = "Северный лесхоз, квадрат 41";

    public const string ArrivalFirst = "Мотор встал у самых ворот. Стрелка лежит на нуле.";
    public const string ArrivalSecond = "В окне горит лампа. Девять дней без связи, а керосин не кончился.";

    public const string GoalLook = "Осмотреть кордон. Найти Савельева.";
    public const string GoalSiphon = "Слить бензин с брошенного седана южнее по просеке. Канистру и шланг взять у крыльца, канистру поставить рядом с машиной, шланг вставить в бак, конец навести на канистру и зажать ЛКМ.";
    public const string GoalPour = "Отнести канистру к своей машине, встать у горловины бака сзади слева и, глядя на машину, зажать E.";
    public const string GoalDrive = "Ехать на север по просеке, к старой стоянке лесорубов.";
    public const string GoalRiver = "Савельев ушёл к реке. Дальше на север.";


    public const string KordonNoteId = "kordon";
    public const string CampNoteId = "camp";

    public const string KordonNote =
        "Кто бы это ни читал.\n\n" +
        "Рация молчит третий день, батареи тут ни при чём.\n" +
        "По ночам кто-то обходит дом. Шаги слышу ясно, а утром следов нет. Пёс не лает, забился под крыльцо и не выходит.\n\n" +
        "Ухожу на север, по просеке, к старой стоянке лесорубов. Там сухо и видно дорогу.\n" +
        "Бочки пустые, всё сжёг за осень. За твоими воротами, южнее по просеке, стоит брошенный седан, в баке у него ещё плещется. Шланг и канистра у крыльца.\n\n" +
        "Ночью не езди.\n\n" +
        "Савельев";

    public const string CampNote =
        "Ночью снова ходили. Теперь уже не один.\n\n" +
        "Сухие стволы у дороги считаю каждое утро. Было четыре, стало семь. Новых пней нет.\n\n" +
        "Иду дальше, к реке. Канистры оставил, мне столько не унести.\n\n" +
        "Не ищи меня ночью.";

    public const string CampArrival = "На стоянке кто-то воткнул кол с красной тряпкой.";

    public const string EndTitle = "КОНЕЦ ПРОЛОГА";
    public const string EndLine = "Лес большой. Дальше сам.";

    public const string RecipesHeader = "Чертежи";
    public const string RecipesEmpty = "пока ни одного";
    public const string NoGoal = "Выжить.";

    public static string RecipeName(string id)
    {
        return id == "fuel_filter" ? "Топливный фильтр" : id;
    }
}

// Весь текст пролога в одном месте.
public static class StoryText
{
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
    public const string GoalSiphon = "Набрать бензин под навесом: взять шланг, навести на канистру у бочек, зажать ЛКМ и отпустить вовремя.";
    public const string GoalPour = "Отнести канистру к машине и, глядя на машину, зажать E.";
    public const string GoalDrive = "Ехать на север по просеке, к старой стоянке лесорубов.";
    public const string GoalRiver = "Савельев ушёл к реке. Дальше на север.";

    public const string KordonNoteId = "kordon";
    public const string CampNoteId = "camp";

    public const string KordonNote =
        "Кто бы это ни читал.\n\n" +
        "Рация молчит третий день, батареи тут ни при чём.\n" +
        "По ночам кто-то обходит дом. Шаги слышу ясно, а утром следов нет. Пёс не лает, забился под крыльцо и не выходит.\n\n" +
        "Ухожу на север, по просеке, к старой стоянке лесорубов. Там сухо и видно дорогу.\n" +
        "Бензин в бочках под навесом, шланг там же. Бери сколько надо.\n\n" +
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

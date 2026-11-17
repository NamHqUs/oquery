namespace OQuery;

public static class SapHanaDialectModule
{
    public static void Register() => SqlDialectRegistry.Register(new SqlDialectRegistration(
        "SapHana",
        ["Hana", "Hdb"],
        () => new SapHanaDialect(),
        Aliases: ["hana"]));
}

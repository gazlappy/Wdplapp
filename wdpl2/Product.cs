namespace Wdpl2;

/// <summary>
/// The product's name as people see it: window title, startup screen, help,
/// and the footer of a published website.
/// </summary>
/// <remarks>
/// This is deliberately separate from the app's internal identity. The
/// assembly name, the application ID (<c>com.wdpl2.app</c>) and the
/// <c>wdpl2</c> data folder decide where a league is stored on disk - change
/// any of those and the app opens empty, with the league still sitting where
/// it was. Renaming the product is this one line; renaming the identity is a
/// migration.
/// <para>
/// Not called <c>AppInfo</c>: MAUI's implicit usings already bring in
/// <c>Microsoft.Maui.ApplicationModel.AppInfo</c>, and the two would clash.
/// </para>
/// </remarks>
public static class Product
{
    public const string Name = "League Manager";

    public const string Version = "2.0.0";
}

namespace Kunai.Web.Client.Pages.Profile;

/// <summary>
/// The profile's tab panels cascade a function, under <see cref="FocusName"/>, that moves the focus to the panel
/// around a component. A component calls it when the control that had the focus goes away (Tekrar dene), so a
/// keyboard or screen reader user goes on from the panel instead of from the top of the page.
/// </summary>
public static class ProfilePanel
{
    public const string FocusName = "ProfilePanelFocus";
}

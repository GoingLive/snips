using Snips.Core.Repositories;

namespace Snips.App;

/// <summary>
/// A real, reviewable starting translation set for German, French and Italian — Switzerland's
/// other national languages, the practical first "other languages" for Roland (docs/SPEC.md's
/// user) even though SupportedLanguages.All lists more. Seeded once, only if there's no actual
/// translation content yet anywhere — matching SeedExampleSnippetsIfEmptyAsync's own "first run
/// only, never overwrite what the user has since touched" rule in App.xaml.cs. Meant to be
/// edited afterward through that same editor, not treated as final or authoritative.
///
/// Deliberately checks actual VariableNameTranslation rows, not just whether any Language row
/// exists — opening Settings -&gt; "Manage translations…" and picking a language from the combo
/// registers that language (AddOrUpdateLanguageAsync) even before anything is typed or saved, so
/// an empty Language shell with zero real translations is not user work worth preserving.
/// </summary>
internal static class LanguagePackSeedData
{
    public static async Task SeedIfEmptyAsync(IVariableTranslationRepository translations)
    {
        foreach (var existing in await translations.ListLanguagesAsync())
        {
            if ((await translations.ListTranslationsAsync(existing.Code)).Count > 0)
                return;
        }

        foreach (var (code, displayName, entries) in Languages)
        {
            await translations.AddOrUpdateLanguageAsync(code, displayName, isRightToLeft: false);
            foreach (var (masterKey, localName) in entries)
                await translations.SetAsync(masterKey, code, localName);
        }
    }

    private static readonly (string Code, string DisplayName, (string MasterKey, string LocalName)[] Entries)[] Languages =
    [
        ("de", "Deutsch",
        [
            ("year", "jahr"), ("year2", "jahr2"), ("month", "monat"), ("day", "tag"),
            ("hour", "stunde"), ("minute", "minute"), ("second", "sekunde"), ("date", "datum"),
            ("time", "zeit"), ("datetime", "datumzeit"), ("iso", "iso"),
            ("localdate", "lokalesdatum"), ("localtime", "lokalezeit"),
            ("locallongdate", "langdatum"), ("locallongtime", "langzeit"),
            ("intldate", "intldatum"), ("now", "jetzt"), ("utcnow", "utcjetzt"),
            ("timestamp", "zeitstempel"), ("timestampms", "zeitstempelms"),
            ("weekday", "wochentag"), ("monthname", "monatsname"), ("week", "woche"),
            ("quarter", "quartal"), ("dayofyear", "jahrestag"), ("daysinmonth", "monatstage"),
            ("tomorrow", "morgen"), ("yesterday", "gestern"), ("timezone", "zeitzone"),
            ("utcoffset", "utcabweichung"), ("snipsversion", "snipsversion"),
            ("user", "benutzer"), ("userfullname", "vollername"), ("useremail", "email"),
            ("machine", "rechnername"), ("domain", "domäne"), ("os", "betriebssystem"),
            ("osversion", "betriebssystemversion"), ("ip", "ip"), ("home", "homeordner"),
            ("desktop", "desktop"), ("documents", "dokumente"), ("downloads", "downloads"),
            ("temp", "temp"), ("appdata", "appdaten"), ("clipboard", "zwischenablage"),
            ("activewindow", "aktivesfenster"), ("activeapp", "aktiveapp"),
            ("snippetname", "snippetname"), ("snippetid", "snippetid"),
            ("snippetdescription", "snippetbeschreibung"), ("usecount", "verwendungen"),
            ("guid", "guid"), ("id", "id"), ("random", "zufallszahl"),
            ("randomstring", "zufallstext"), ("counter", "zähler"),
        ]),

        ("fr", "Français",
        [
            ("year", "annee"), ("year2", "annee2"), ("month", "mois"), ("day", "jour"),
            ("hour", "heure"), ("minute", "minute"), ("second", "seconde"), ("date", "date"),
            ("time", "horaire"), ("datetime", "dateheure"), ("iso", "iso"),
            ("localdate", "datelocale"), ("localtime", "heurelocale"),
            ("locallongdate", "datelongue"), ("locallongtime", "heurelongue"),
            ("intldate", "dateintl"), ("now", "maintenant"), ("utcnow", "utcmaintenant"),
            ("timestamp", "horodatage"), ("timestampms", "horodatagems"),
            ("weekday", "joursemaine"), ("monthname", "nommois"), ("week", "semaine"),
            ("quarter", "trimestre"), ("dayofyear", "jourannee"), ("daysinmonth", "joursmois"),
            ("tomorrow", "demain"), ("yesterday", "hier"), ("timezone", "fuseau"),
            ("utcoffset", "decalageutc"), ("snipsversion", "versionsnips"),
            ("user", "utilisateur"), ("userfullname", "nomcomplet"), ("useremail", "email"),
            ("machine", "ordinateur"), ("domain", "domaine"), ("os", "systeme"),
            ("osversion", "versionsysteme"), ("ip", "ip"), ("home", "dossierperso"),
            ("desktop", "bureau"), ("documents", "documents"), ("downloads", "telechargements"),
            ("temp", "temp"), ("appdata", "donneesapp"), ("clipboard", "pressepapiers"),
            ("activewindow", "fenetreactive"), ("activeapp", "appactive"),
            ("snippetname", "nomsnippet"), ("snippetid", "idsnippet"),
            ("snippetdescription", "descriptionsnippet"), ("usecount", "utilisations"),
            ("guid", "guid"), ("id", "id"), ("random", "aleatoire"),
            ("randomstring", "textealeatoire"), ("counter", "compteur"),
        ]),

        ("it", "Italiano",
        [
            ("year", "anno"), ("year2", "anno2"), ("month", "mese"), ("day", "giorno"),
            ("hour", "ora"), ("minute", "minuto"), ("second", "secondo"), ("date", "data"),
            ("time", "orario"), ("datetime", "dataora"), ("iso", "iso"),
            ("localdate", "datalocale"), ("localtime", "oralocale"),
            ("locallongdate", "datalunga"), ("locallongtime", "oralunga"),
            ("intldate", "dataintl"), ("now", "adesso"), ("utcnow", "utcadesso"),
            ("timestamp", "marcatemporale"), ("timestampms", "marcatemporalems"),
            ("weekday", "giornosettimana"), ("monthname", "nomemese"), ("week", "settimana"),
            ("quarter", "trimestre"), ("dayofyear", "giornoanno"), ("daysinmonth", "giornimese"),
            ("tomorrow", "domani"), ("yesterday", "ieri"), ("timezone", "fusoorario"),
            ("utcoffset", "offsetutc"), ("snipsversion", "versionesnips"),
            ("user", "utente"), ("userfullname", "nomecompleto"), ("useremail", "email"),
            ("machine", "computer"), ("domain", "dominio"), ("os", "sistema"),
            ("osversion", "versionesistema"), ("ip", "ip"), ("home", "cartellahome"),
            ("desktop", "desktop"), ("documents", "documenti"), ("downloads", "download"),
            ("temp", "temp"), ("appdata", "datiapp"), ("clipboard", "appunti"),
            ("activewindow", "finestraattiva"), ("activeapp", "appattiva"),
            ("snippetname", "nomesnippet"), ("snippetid", "idsnippet"),
            ("snippetdescription", "descrizionesnippet"), ("usecount", "utilizzi"),
            ("guid", "guid"), ("id", "id"), ("random", "casuale"),
            ("randomstring", "testocasuale"), ("counter", "contatore"),
        ]),
    ];
}

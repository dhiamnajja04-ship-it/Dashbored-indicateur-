using System.Globalization;
using System.Text;
using IaService.Models;

namespace IaService.Services;

/// <summary>
/// Construit le prompt envoyé au modèle local à partir des indicateurs validés.
/// Tout ce que le modèle « sait » vient d'ici : s'il n'est pas dans ce texte,
/// il n'est pas dans la réponse (exigence semaine 8).
/// </summary>
public static class PromptBuilder
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    public static string Construire(IReadOnlyList<IndicateurValide> indicateurs, string? question)
    {
        var sb = new StringBuilder();

        sb.AppendLine(
            "Tu es un analyste de données publiques. Tu rédiges en français, de façon factuelle et concise."
        );
        sb.AppendLine();
        sb.AppendLine("RÈGLES STRICTES :");
        sb.AppendLine("- Utilise UNIQUEMENT les données listées ci-dessous. Elles sont toutes validées.");
        sb.AppendLine("- N'invente aucun chiffre, aucune date, aucun indicateur absent de la liste.");
        sb.AppendLine("- Si la liste ne permet pas de répondre, dis-le explicitement.");
        sb.AppendLine("- Cite les valeurs avec leur unité.");
        sb.AppendLine("- Précise toujours le territoire d'une valeur ; ne compare jamais un gouvernorat avec un total national comme s'ils étaient de même nature.");
        sb.AppendLine("- La ligne « Position » donne déjà l'écart à la cible : recopie-la, ne recalcule rien.");
        sb.AppendLine("- N'attribue jamais la valeur d'un indicateur à un autre : chaque chiffre appartient à l'indicateur sous lequel il est listé.");
        sb.AppendLine("- Ne déduis aucune corrélation entre deux indicateurs : ils mesurent des choses différentes.");
        sb.AppendLine();
        sb.AppendLine("=== DONNÉES VALIDÉES ===");

        foreach (var indicateur in indicateurs)
        {
            sb.Append("Indicateur « ").Append(indicateur.Nom).Append(" » (code ").Append(indicateur.Code).Append(')');
            if (!string.IsNullOrWhiteSpace(indicateur.Unite))
            {
                sb.Append(", unité : ").Append(indicateur.Unite);
            }
            if (!string.IsNullOrWhiteSpace(indicateur.Frequence))
            {
                sb.Append(", fréquence : ").Append(indicateur.Frequence);
            }
            sb.AppendLine();

            if (!string.IsNullOrWhiteSpace(indicateur.Description))
            {
                sb.Append("  Description : ").AppendLine(indicateur.Description);
            }
            if (indicateur.ValeurCible.HasValue)
            {
                sb.Append("  Valeur cible : ")
                    .Append(indicateur.ValeurCible.Value.ToString("0.##", Fr));
                if (indicateur.AnneeReference.HasValue)
                {
                    sb.Append(" (année de référence ").Append(indicateur.AnneeReference.Value).Append(')');
                }
                sb.AppendLine();
            }

            sb.AppendLine("  Valeurs validées :");
            foreach (var valeur in indicateur.Valeurs)
            {
                sb.Append("    - ")
                    .Append(valeur.Valeur.ToString("0.##", Fr))
                    .Append(' ')
                    .Append(indicateur.Unite);

                // Le territoire est indiqué avant le reste : sans lui, le modèle
                // confond une valeur nationale et une valeur de gouvernorat.
                var territoire = string.IsNullOrWhiteSpace(valeur.Gouvernorat)
                    ? (string.IsNullOrWhiteSpace(valeur.Pays) ? null : $"{valeur.Pays} (niveau national)")
                    : $"{valeur.Gouvernorat}, {valeur.Pays}".TrimEnd(',', ' ');

                if (territoire is not null)
                {
                    sb.Append(" — ").Append(territoire);
                }

                // Libellés plutôt qu'identifiants : « organisation #1 » n'apprend
                // rien au modèle, qui finissait par le recracher tel quel.
                sb.Append(" (source : ")
                    .Append(string.IsNullOrWhiteSpace(valeur.OrganisationNom)
                        ? $"organisation #{valeur.OrganisationId}"
                        : valeur.OrganisationNom)
                    .Append(", période : ")
                    .Append(string.IsNullOrWhiteSpace(valeur.PeriodeLibelle)
                        ? $"#{valeur.PeriodeId}"
                        : valeur.PeriodeLibelle);
                if (!string.IsNullOrWhiteSpace(valeur.DegreDeFiabilite))
                {
                    sb.Append(", fiabilité : ").Append(valeur.DegreDeFiabilite);
                }
                sb.AppendLine(")");

                // L'écart à la cible est CALCULÉ ici, pas laissé au modèle.
                // Un petit modèle compare mal deux nombres : il écrivait
                // « 12,4 est en dessous de la cible 10 ». En lui donnant la
                // conclusion, il n'a plus qu'à la rapporter.
                if (indicateur.ValeurCible.HasValue && indicateur.ValeurCible.Value != 0)
                {
                    var ecart = valeur.Valeur - indicateur.ValeurCible.Value;
                    var sens = ecart switch
                    {
                        > 0 => "AU-DESSUS de la cible",
                        < 0 => "EN DESSOUS de la cible",
                        _ => "ÉGAL à la cible",
                    };
                    sb.Append("      Position : ")
                      .Append(sens)
                      .Append(" de ")
                      .Append(Math.Abs(ecart).ToString("0.##", Fr))
                      .Append(' ')
                      .Append(indicateur.Unite)
                      .Append(" (cible ")
                      .Append(indicateur.ValeurCible.Value.ToString("0.##", Fr))
                      .AppendLine(").");
                }

                if (!string.IsNullOrWhiteSpace(valeur.Commentaire))
                {
                    sb.Append("      Commentaire : ").AppendLine(valeur.Commentaire);
                }
            }

            sb.AppendLine();
        }

        sb.AppendLine("=== FIN DES DONNÉES ===");
        sb.AppendLine();

        if (string.IsNullOrWhiteSpace(question))
        {
            // Une tendance suppose au moins deux mesures du même indicateur.
            // Demander « les tendances » sur une valeur unique poussait le
            // modèle à en inventer une (« en hausse par rapport à 2025 »).
            var plusieursMesures = indicateurs.Any(i => i.Valeurs.Count > 1);

            sb.AppendLine("TÂCHE : rédige une synthèse de 5 à 10 lignes.");
            sb.AppendLine("- Traite chaque indicateur séparément, dans l'ordre de la liste.");
            sb.AppendLine("- Pour chacun : rappelle sa valeur, son unité, son territoire, puis recopie sa ligne « Position ».");

            if (plusieursMesures)
            {
                sb.AppendLine("- Indique une évolution UNIQUEMENT pour les indicateurs comptant plusieurs valeurs.");
            }
            else
            {
                sb.AppendLine("- Chaque indicateur ne compte qu'UNE seule mesure : ne parle NI de tendance, NI d'évolution, NI de hausse ou de baisse dans le temps.");
            }

            sb.AppendLine("- Termine par 2 ou 3 points à surveiller, en une phrase chacun.");
            sb.AppendLine("- Dans cette conclusion, n'introduis AUCUN chiffre qui ne figure pas dans les données ci-dessus.");
        }
        else
        {
            sb.Append("QUESTION DE L'UTILISATEUR : ").AppendLine(question.Trim());
            sb.AppendLine("Réponds à cette question en te basant exclusivement sur les données ci-dessus.");
        }

        return sb.ToString();
    }
}

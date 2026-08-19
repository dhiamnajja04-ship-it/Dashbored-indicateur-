using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("indicateurs")]
public class Indicateur
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("nom_indicateur")]
    public string NomIndicateur { get; set; } = string.Empty;

    [Column("valeur")]
    public double Valeur { get; set; }

    [Column("periode")]
    public string Periode { get; set; } = string.Empty;

    [Column("organisation")]
    public string Organisation { get; set; } = string.Empty;
}

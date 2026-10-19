namespace GoDental.Domain.Rules;
public static class OrderRules
{
    public static bool CanTransition(string from, string to) => from switch
    {
        "RECIBIDO" => to is "PREPARANDO" or "CANCELADO" or "INCIDENCIA",
        "PREPARANDO" => to is "LISTO" or "CANCELADO" or "INCIDENCIA",
        "LISTO" => to is "EN_RUTA" or "ENTREGADO" or "CANCELADO" or "INCIDENCIA",
        "EN_RUTA" => to is "ENTREGADO" or "INCIDENCIA",
        "INCIDENCIA" => to is "PREPARANDO" or "LISTO" or "EN_RUTA" or "CANCELADO",
        _ => false
    };
}

namespace GoDental.Application.DTOs;

// Este perfil no contiene credenciales ni datos de negocio.
public sealed record WorkstationSettings(string Server = "", string Database = "", string Theme = "Light");

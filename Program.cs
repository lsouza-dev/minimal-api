using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using minimal_api.Dominio.DTOs;
using minimal_api.Dominio.Entidades;
using minimal_api.Dominio.Enuns;
using minimal_api.Dominio.Interfaces;
using minimal_api.Dominio.ModelViews;
using minimal_api.Dominio.Servicos;
using minimal_api.Infraestrutura.Db;

#region  Builder
var builder = WebApplication.CreateBuilder(args);

var key = builder.Configuration.GetSection("Jwt").ToString();
if (string.IsNullOrEmpty(key) || key.Length < 16) key = "1234567890123456";

builder.Services.AddAuthentication(option =>
{
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(option =>
{
    option.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ValidateIssuer = false,
        ValidateAudience = false 
    };
});

builder.Services.AddAuthorization();

builder.Services.AddScoped<IAdministradorServico, AdministradorServico>();
builder.Services.AddScoped<IVeiculoServico, VeiculoServico>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => {
    options.AddSecurityDefinition("Bearer",new OpenApiSecurityScheme{
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat  = "JWT",
        In = ParameterLocation.Header,
        Description = "Insira o token JWT aqui"

    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme{
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

builder.Services.AddDbContext<DbContexto>(options =>
options.UseMySql(
    builder.Configuration.GetConnectionString("MySql"),
    ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("MySql"))
));



var app = builder.Build();
#endregion

#region Home
app.MapGet("/", () => Results.Json(new Home())).AllowAnonymous().WithTags("Home");
#endregion

#region Administradores


string  GerarTokenJwt(Administrador adm)
{
    if (string.IsNullOrEmpty(key)) return string.Empty;

    var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

    var claims = new List<Claim>(){
        new Claim("Email", adm.Email),
        new Claim("Perfil",adm.Perfil),
        new Claim(ClaimTypes.Role,adm.Perfil)
    };

    var token = new JwtSecurityToken(
        claims: claims,
        expires: DateTime.Now.AddDays(1),
        signingCredentials: credentials
    );

    return new JwtSecurityTokenHandler().WriteToken(token);
}



app.MapPost("/administradores/login", ([FromBody] LoginDTO loginDTO, IAdministradorServico administradorServico) =>
{
    var adm = administradorServico.Login(loginDTO);
    if (adm != null)
    {
        string token = GerarTokenJwt(adm);

        return Results.Ok(new AdministradorLogado
        {
            Email = adm.Email,
            Perfil = adm.Perfil,
            Token = token
        });
    }
    else
        return Results.Unauthorized();
}).AllowAnonymous().WithTags("Administradores");





app.MapGet("/administradores", ([FromQuery] int? pagina, IAdministradorServico administradorServico) =>
{
    var adms = new List<AdministradorModelView>();
    var administradores = administradorServico.Todos(pagina);
    foreach (var adm in administradores)
    {
        adms.Add(new AdministradorModelView
        {
            Id = adm.Id,
            Email = adm.Email,
            Perfil = adm.Perfil
        });
    }

    return Results.Ok(adms);
}).RequireAuthorization()
.RequireAuthorization(new AuthorizeAttribute{Roles = "Adm"})
.WithTags("Administradores");






app.MapPost("/administradores", ([FromBody] AdministradorDTO administradorDTO, IAdministradorServico administradorServico) =>
{

    var validacao = new ErrosDeValidacao
    {
        Mensagems = new List<string>()
    };

    if (string.IsNullOrEmpty(administradorDTO.Email)) validacao.Mensagems.Add("O Email não pode estar vazio");

    if (string.IsNullOrEmpty(administradorDTO.Senha)) validacao.Mensagems.Add("A Senha não pode estar vazia.");

    if (administradorDTO.Perfil == null) validacao.Mensagems.Add("O Perfil não pode estar vazio");

    if (validacao.Mensagems.Count > 0) return Results.BadRequest(validacao);


    var adm = new Administrador
    {
        Email = administradorDTO.Email,
        Senha = administradorDTO.Senha,
        Perfil = administradorDTO.Perfil.ToString() ?? Perfil.Editor.ToString()
    };

    administradorServico.Incluir(adm);
    return Results.Created($"/administradores/{adm.Id}", new AdministradorModelView
    {
        Id = adm.Id,
        Email = adm.Email,
        Perfil = adm.Perfil
    });

}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm"})
.WithTags("Administradores");






app.MapGet("/Administradores/{id}", ([FromRoute] int id, IAdministradorServico administradorServico) =>
{
    var adm = administradorServico.BuscaPorId(id);
    if (adm == null) Results.NotFound();
    return Results.Ok(new AdministradorModelView
    {
        Id = adm.Id,
        Email = adm.Email,
        Perfil = adm.Perfil
    });
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm"})
.WithTags("Administradores");


#endregion

#region  Veiculos

ErrosDeValidacao validaDTO(VeiculoDTO veicDTO)
{

    var validacao = new ErrosDeValidacao
    {
        Mensagems = new List<string>()
    };

    if (string.IsNullOrEmpty(veicDTO.Nome))
        validacao.Mensagems.Add("O Nome não pode ser vazio.");

    if (string.IsNullOrEmpty(veicDTO.Marca))
        validacao.Mensagems.Add("A Marca não pode ficar em branco.");

    if (veicDTO.Ano < 1950)
        validacao.Mensagems.Add("Veiculo muito antigo, são aceito apenas carros de ano acima de 1950.");

    return validacao;
}

app.MapPost("/veiculos", ([FromBody] VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
{



    var validacao = validaDTO(veiculoDTO);

    if (validacao.Mensagems.Count > 0)
        return Results.BadRequest(validacao);


    var veiculo = new Veiculo
    {
        Nome = veiculoDTO.Nome,
        Marca = veiculoDTO.Marca,
        Ano = veiculoDTO.Ano
    };

    veiculoServico.Incluir(veiculo);

    return Results.Created($"/veiculo{veiculo.Id}", veiculo);
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm,Editor"})
.WithTags("Administradores");





app.MapGet("/veiculos", ([FromQuery] int? pagina, IVeiculoServico veiculoServico) =>
{
    var veiculos = veiculoServico.Todos(pagina);

    return Results.Ok(veiculos);
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm,Editor"})
.WithTags("Administradores");




app.MapGet("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
{
    var veiculo = veiculoServico.BuscaPorId(id);
    if (veiculo == null) Results.NotFound();
    return Results.Ok(veiculo);
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm,Editor"})
.WithTags("Administradores");


app.MapPut("/veiculos/{id}", ([FromRoute] int id, VeiculoDTO veiculoDTO, IVeiculoServico veiculoServico) =>
{


    var veiculo = veiculoServico.BuscaPorId(id);
    if (veiculo == null) Results.NotFound();

    var validacao = validaDTO(veiculoDTO);

    if (validacao.Mensagems.Count > 0)
        return Results.BadRequest(validacao);

    veiculo.Nome = veiculoDTO.Nome;
    veiculo.Marca = veiculoDTO.Marca;
    veiculo.Ano = veiculoDTO.Ano;

    veiculoServico.Atualizar(veiculo);

    return Results.Ok(veiculo);
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm"})
.WithTags("Administradores");

app.MapDelete("/veiculos/{id}", ([FromRoute] int id, IVeiculoServico veiculoServico) =>
{
    var veiculo = veiculoServico.BuscaPorId(id);
    if (veiculo == null) Results.NotFound();
    veiculoServico.Apagar(veiculo);
    return Results.NoContent();
}).RequireAuthorization().
RequireAuthorization(new AuthorizeAttribute{Roles = "Adm"})
.WithTags("Administradores");


#endregion

#region App
app.UseSwagger();
app.UseSwaggerUI();

app.UseAuthentication();
app.UseAuthorization();

app.Run();
#endregion
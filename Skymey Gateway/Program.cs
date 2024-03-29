using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using System.Text.Json.Serialization;
using Skymey_Gateway;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.OpenApi.Models;
using Skymey_Gateway.Data;
using Microsoft.Extensions.Configuration;
using System.Net;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using System.Security.Cryptography.X509Certificates;
using Skymey_Gateway.Actions.JWT;
using Skymey_main_lib.Interfaces.JWT;

var builder = WebApplication.CreateBuilder(args);
string connection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationContext>(options => options.UseSqlServer(connection));
builder.Services.AddRazorPages();
builder.Services.AddCors(o => o.AddPolicy("MyPolicy", builder =>
{
    builder.AllowAnyMethod()
           .AllowAnyHeader()
           .AllowAnyOrigin();
}));

var httpsConnectionAdapterOptions = new HttpsConnectionAdapterOptions
{
    SslProtocols = System.Security.Authentication.SslProtocols.Tls12,
    ClientCertificateMode = ClientCertificateMode.AllowCertificate,
    ServerCertificate = new X509Certificate2("certificate_cert_out.pfx", "1234")

};
builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = (int)HttpStatusCode.TemporaryRedirect;
    options.HttpsPort = 5001;
});
builder.Services.AddControllers();
builder.Services.AddAuthorization();
builder.Services.AddSwaggerGen(option => {
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "Skymey Gateway", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Please enter token",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type=ReferenceType.SecurityScheme,
                    Id="Bearer"
                }
            }, new string[]{}
        }
    });
});
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Listen(IPAddress.Parse("192.168.1.85"), 5002);
    serverOptions.Listen(IPAddress.Parse("192.168.1.85"), 5003, listenOptions =>
    {
        listenOptions.UseHttps("certificate_cert_out.pfx", "1234");
    });
});
builder.WebHost.UseUrls("http://192.168.1.85:5002;https://192.168.1.85:5003;");
#region Auth
builder.Services.AddOptions();
builder.Services.Configure<JWTSettings>(builder.Configuration.GetSection("JWTSettings")); // ������������� JWTSettings � ������ ������������ appsettings.json

var secretKey = builder.Configuration.GetSection("JWTSettings:SecretKey").Value; // ��������� ��� �� appsettings.json
var issuer = builder.Configuration.GetSection("JWTSettings:Issuer").Value; // �������� ������. ����� ������� ����� ��������
var audience = builder.Configuration.GetSection("JWTSettings:Audience").Value; // ������������ ������. ����� ������� ����� ��������

var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));


builder.Services.AddAuthentication(option => { // ��������� �������������� � ������� �������
    option.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    option.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(option => {
    option.TokenValidationParameters = new TokenValidationParameters
    { // ������ ��������� ��������� ������. ����� ���������: ��������, �����������, ����, ���� ��������
        ValidateIssuer = true,
        ValidIssuer = issuer,
        ValidateAudience = true,
        ValidAudience = audience,
        ValidateLifetime = true,
        IssuerSigningKey = signingKey,
        ValidateIssuerSigningKey = true,
        LifetimeValidator = CustomLifetime.CustomLifetimeValidator
    };
});
#endregion
builder.Services.AddTransient<ITokenService, CreateJWTToken>();
var app = builder.Build();

app.UseHttpsRedirection().UseCertificateForwarding().UseCors().UseHsts();
app.UseStaticFiles();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("MyPolicy");
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.MapControllers();
app.Run();

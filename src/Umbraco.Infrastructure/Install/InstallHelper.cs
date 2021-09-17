using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Configuration;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Install.Models;
using Umbraco.Cms.Core.Net;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Migrations.Install;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Extensions;
using Constants = Umbraco.Cms.Core.Constants;

namespace Umbraco.Cms.Infrastructure.Install
{
    public sealed class InstallHelper
    {
        private readonly DatabaseBuilder _databaseBuilder;
        private readonly ILogger<InstallHelper> _logger;
        private readonly IUmbracoVersion _umbracoVersion;
        private readonly IOptionsMonitor<ConnectionStrings> _connectionStrings;
        private readonly IInstallationService _installationService;
        private readonly ICookieManager _cookieManager;
        private readonly IUserAgentProvider _userAgentProvider;
        private readonly IUmbracoDatabaseFactory _umbracoDatabaseFactory;
        private readonly IOptionsMonitor<GlobalSettings> _globalSettings;
        private InstallationType? _installationType;

        public InstallHelper(DatabaseBuilder databaseBuilder,
            ILogger<InstallHelper> logger,
            IUmbracoVersion umbracoVersion,
            IOptionsMonitor<ConnectionStrings> connectionStrings,
            IInstallationService installationService,
            ICookieManager cookieManager,
            IUserAgentProvider userAgentProvider,
            IUmbracoDatabaseFactory umbracoDatabaseFactory,
            IOptionsMonitor<GlobalSettings> globalSettings)
        {
            _logger = logger;
            _umbracoVersion = umbracoVersion;
            _databaseBuilder = databaseBuilder;
            _connectionStrings = connectionStrings;
            _installationService = installationService;
            _cookieManager = cookieManager;
            _userAgentProvider = userAgentProvider;
            _umbracoDatabaseFactory = umbracoDatabaseFactory;
            _globalSettings = globalSettings;

            // We need to initialize the type already, as we can't detect later, if the connection string is added on the fly.
            GetInstallationType();
        }

        public InstallationType GetInstallationType() => _installationType ??= IsBrandNewInstall ? InstallationType.NewInstall : InstallationType.Upgrade;

        public async Task SetInstallStatusAsync(bool isCompleted, string errorMsg)
        {
            try
            {
                var userAgent = _userAgentProvider.GetUserAgent();

                // Check for current install ID
                var installCookie = _cookieManager.GetCookieValue(Constants.Web.InstallerCookieName);
                if (!Guid.TryParse(installCookie, out var installId))
                {
                    installId = Guid.NewGuid();

                    _cookieManager.SetCookieValue(Constants.Web.InstallerCookieName, installId.ToString());
                }

                var dbProvider = string.Empty;
                if (IsBrandNewInstall == false)
                {
                    // we don't have DatabaseProvider anymore... doing it differently
                    //dbProvider = ApplicationContext.Current.DatabaseContext.DatabaseProvider.ToString();
                    dbProvider = _umbracoDatabaseFactory.SqlContext.SqlSyntax.DbProvider;
                }

                var installLog = new InstallLog(installId: installId, isUpgrade: IsBrandNewInstall == false,
                    installCompleted: isCompleted, timestamp: DateTime.Now, versionMajor: _umbracoVersion.Version.Major,
                    versionMinor: _umbracoVersion.Version.Minor, versionPatch: _umbracoVersion.Version.Build,
                    versionComment: _umbracoVersion.Comment, error: errorMsg, userAgent: userAgent,
                    dbProvider: dbProvider);

                await _installationService.LogInstall(installLog);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in InstallStatus trying to check upgrades");
            }
        }

        /// <summary>
        /// Checks if this is a brand new install, meaning that there is no configured database connection or the database is empty.
        /// </summary>
        /// <value>
        ///   <c>true</c> if this is a brand new install; otherwise, <c>false</c>.
        /// </value>
        private bool IsBrandNewInstall => _connectionStrings.CurrentValue.UmbracoConnectionString?.IsConnectionStringConfigured() != true ||
                    _databaseBuilder.IsDatabaseConfigured == false ||
                    (_globalSettings.CurrentValue.InstallMissingDatabase && _databaseBuilder.CanConnectToDatabase == false) ||
                    _databaseBuilder.IsUmbracoInstalled() == false;
    }
}

﻿using System;
using System.Reflection;
using System.Web;
using DotNetNuke.Common;
using DotNetNuke.Common.Utilities;
using DotNetNuke.ComponentModel;
using DotNetNuke.Data;
using DotNetNuke.Entities.Portals;
using DotNetNuke.Entities.Users;
using DotNetNuke.Entities.Users.Membership;
using DotNetNuke.Modules.HTMLEditorProvider;
using DotNetNuke.Modules.NavigationProvider;
using DotNetNuke.Security.Membership;
using DotNetNuke.Security.Permissions;
using DotNetNuke.Security.Profile;
using DotNetNuke.Security.Roles;
using DotNetNuke.Services.Cache;
using DotNetNuke.Services.ClientCapability;
using DotNetNuke.Services.Cryptography;
using DotNetNuke.Services.FileSystem;
using DotNetNuke.Services.Log.EventLog;
using DotNetNuke.Services.ModuleCache;
using DotNetNuke.Services.OutputCache;
using DotNetNuke.Services.Scheduling;
using DotNetNuke.Services.Search;
using DotNetNuke.Services.Sitemap;
using DotNetNuke.Services.Url.FriendlyUrl;
using DotNetNuke.Tests.Utilities;
using DotNetNuke.Tests.Utilities.Mocks;
using Moq;
using NUnit.Framework;

namespace DotNetNuke.Tests.Core.Providers.Membership
{
    [TestFixture]
    public class MembershipProviderTests : DnnUnitTest
    {
        [SetUp]
        public void SetUp()
        {
            Globals.SetStatus(Globals.UpgradeStatus.None);

            ComponentFactory.Container = new SimpleContainer();
            ComponentFactory.InstallComponents(new ProviderInstaller("data", typeof(DataProvider), typeof(SqlDataProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("caching", typeof(CachingProvider), typeof(FBCachingProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("logging", typeof(LoggingProvider), typeof(DBLoggingProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("members", typeof(MembershipProvider), typeof(AspNetMembershipProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("roles", typeof(RoleProvider), typeof(DNNRoleProvider)));
            ComponentFactory.InstallComponents(new ProviderInstaller("profiles", typeof(ProfileProvider), typeof(DNNProfileProvider)));
            ComponentFactory.RegisterComponent<IPortalSettingsController, PortalSettingsController>();

            PortalController.ClearInstance();
            UserController.ClearInstance();
            RoleController.ClearInstance();

            var roleController = RoleController.Instance;
            var roleProviderField = roleController.GetType().GetField("provider", BindingFlags.NonPublic | BindingFlags.Static);
            if (roleProviderField != null)
            {
                roleProviderField.SetValue(roleController, RoleProvider.Instance());
            }

            var membershipType = typeof(System.Web.Security.Membership);
            var initializedDefaultProviderField = membershipType.GetField("s_InitializedDefaultProvider", BindingFlags.NonPublic | BindingFlags.Static);
            var defaultProviderField = membershipType.GetField("s_Provider", BindingFlags.NonPublic | BindingFlags.Static);
            if (initializedDefaultProviderField != null
                && defaultProviderField != null
                && (bool)initializedDefaultProviderField.GetValue(null) == false)
            {
                initializedDefaultProviderField.SetValue(null, true);
                defaultProviderField.SetValue(null, System.Web.Security.Membership.Providers["AspNetSqlMembershipProvider"]);
            }
        }

        [TearDown]
        public void TearDown()
        {
        }

        //TODO: Must be moved to integration tests.
        //Note: this is the only test in core unit testing project that requires an active site to run.
        [Test]
        public void Password_Should_Saved_In_History_During_Create_User()
        {
            var username = $"{Constants.RuFirstName}{DateTime.Now.Ticks}";
            var email = $"{username}@dnn.com";

            var user = new UserInfo
            {
                PortalID = Constants.PORTAL_Zero,
                UserID = Null.NullInteger,
                Username = username,
                Email = email,
                FirstName = username,
                LastName = string.Empty,
                Membership = new UserMembership
                {
                    Approved = true,
                    Password = Constants.DefaultPassword
                }

            };
            var status = UserController.CreateUser(ref user);

            Assert.AreEqual(UserCreateStatus.Success, status);

            var simulator = new Instance.Utilities.HttpSimulator.HttpSimulator("/", AppDomain.CurrentDomain.BaseDirectory);
            simulator.SimulateRequest(new Uri(WebsiteAppPath));
            HttpContextBase httpContextBase = new HttpContextWrapper(HttpContext.Current);
            HttpContextSource.RegisterInstance(httpContextBase);

            var isPasswordInHistory = new MembershipPasswordController().IsPasswordInHistory(user.UserID, user.PortalID, user.Membership.Password);

            Assert.AreEqual(true, isPasswordInHistory);
        }

        private static void RegisterIfNotAlreadyRegistered<TConcrete>() where TConcrete : class, new()
        {
            RegisterIfNotAlreadyRegistered<TConcrete, TConcrete>("");
        }

        private static void RegisterIfNotAlreadyRegistered<TAbstract, TConcrete>(string name)
           where TAbstract : class
           where TConcrete : class, new()
        {
            var provider = ComponentFactory.GetComponent<TAbstract>();
            if (provider == null)
            {
                if (String.IsNullOrEmpty(name))
                {
                    ComponentFactory.RegisterComponentInstance<TAbstract>(new TConcrete());
                }
                else
                {
                    ComponentFactory.RegisterComponentInstance<TAbstract>(name, new TConcrete());
                }
            }
        }
    }
}

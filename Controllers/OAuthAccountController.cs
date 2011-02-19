﻿using System;
using System.Linq;
using System.Collections.Generic;
using System.Web.Mvc;
using NGM.OpenAuthentication.Core;
using NGM.OpenAuthentication.Core.OAuth;
using NGM.OpenAuthentication.Extensions;
using NGM.OpenAuthentication.Models;
using NGM.OpenAuthentication.ViewModels;
using Orchard;
using Orchard.Localization;
using Orchard.Themes;

namespace NGM.OpenAuthentication.Controllers {
    [Themed]
    public class OAuthAccountController : Controller {
        private readonly IEnumerable<IOAuthAuthorizer> _oAuthWrappers;
        private readonly IOrchardServices _orchardServices;

        public OAuthAccountController(IEnumerable<IOAuthAuthorizer> oAuthWrappers, IOrchardServices orchardServices) {
            _oAuthWrappers = oAuthWrappers;
            _orchardServices = orchardServices;
            T = NullLocalizer.Instance;
        }

        public Localizer T { get; set; }

        public ActionResult LogOn(string returnUrl, string knownProvider) {
            var viewModel = new CreateViewModel();
            TryUpdateModel(viewModel);

            if (string.IsNullOrEmpty(viewModel.KnownProvider)) {
                if (_orchardServices.WorkContext.HttpContext.Session["knownProvider"] != null) {
                    viewModel.KnownProvider = _orchardServices.WorkContext.HttpContext.Session["knownProvider"] as string;
                }
                else if (!string.IsNullOrEmpty(knownProvider)) {
                    viewModel.KnownProvider = knownProvider;
                }
            }

            var wrapper = _oAuthWrappers
                .Where(o => o.Provider.ToString().ToLowerInvariant() == viewModel.KnownProvider.ToLowerInvariant() && o.IsConsumerConfigured).FirstOrDefault();

            if (wrapper != null) {
                var result = wrapper.Authorize(returnUrl);

                if (result.AuthenticationStatus == OpenAuthenticationStatus.ErrorAuthenticating) {
                    this.AddError(result.Error.Key, T(result.Error.Value));
                    return DefaultLogOnResult(returnUrl);
                }
                if (result.AuthenticationStatus == OpenAuthenticationStatus.RequiresRegistration) {
                    TempData["registermodel"] = result.RegisterModel;
                    return DefaultRegisterResult(returnUrl, result.RegisterModel);
                }

                if (result.Result != null) return result.Result;
            }

            return DefaultLogOnResult(returnUrl);
        }

        private ActionResult DefaultLogOnResult(string returnUrl) {
            return RedirectToAction("LogOn", "Account", new { area = "Orchard.Users", ReturnUrl = returnUrl });
        }

        private ActionResult DefaultRegisterResult(string returnUrl, RegisterModel model) {
            return RedirectToAction("Register", "Account", RouteValuesHelper.CreateRegisterRouteValueDictionary(returnUrl, model));
        }
    }
}
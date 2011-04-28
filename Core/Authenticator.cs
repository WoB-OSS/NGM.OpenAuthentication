﻿using System;
using System.Collections.Generic;
using NGM.OpenAuthentication.Services;
using Orchard;
using Orchard.ContentManagement;
using Orchard.Security;
using Orchard.Users.Models;

namespace NGM.OpenAuthentication.Core {
    public class Authenticator : IAuthenticator {
        private readonly IAuthenticationService _authenticationService;
        private readonly IOpenAuthenticationService _openAuthenticationService;
        private readonly IMembershipService _membershipService;
        private readonly IOrchardServices _orchardServices;

        public Authenticator(IAuthenticationService authenticationService,
                              IOpenAuthenticationService openAuthenticationService,
                              IMembershipService membershipService,
                              IOrchardServices orchardServices) {
            _authenticationService = authenticationService;
            _openAuthenticationService = openAuthenticationService;
            _membershipService = membershipService;
            _orchardServices = orchardServices;
        }

        public AuthenticationResult Authorize(OpenAuthenticationParameters parameters) {
            var userFound = _openAuthenticationService.GetUser(parameters);

            var userLoggedIn = _authenticationService.GetAuthenticatedUser();

            if (AccountAlreadyExistsAndUserIsLoggedOn(userFound, userLoggedIn)) {
                if (AccountIsAssignedToLoggedOnAccount(userFound, userLoggedIn)) {
                    // The person is trying to log in as himself.. bit weird
                    return new AuthenticationResult(OpenAuthenticationStatus.Authenticated);
                }

                return new AuthenticationResult(OpenAuthenticationStatus.ErrorAuthenticating, 
                    new KeyValuePair<string, string>("AccountAssigned", "Account is already assigned"));
            }
            if (AccountDoesNotExistAndUserIsNotLoggedOn(userFound, userLoggedIn)) {
                // If I am not logged in, and I noone has this identifier, then go to register page to get them to confirm details.
                var registrationSettings = _orchardServices.WorkContext.CurrentSite.As<RegistrationSettingsPart>();

                State.Parameters = parameters;

                if (AutoRegistrationIsEnabled(registrationSettings)) {
                    if (CanCreateAccount(parameters)) {
                        userFound = CreateUser(parameters);
                    }
                    else
                    {
                        return new AuthenticationResult(OpenAuthenticationStatus.AssociateOnLogon,
                            new KeyValuePair<string, string>("AccessDenied", "User does not have enough details to auto create account"));
                    }
                } else if (RegistrationIsEnabled(registrationSettings)) {
                    return new AuthenticationResult(OpenAuthenticationStatus.AssociateOnLogon);
                } else {
                    return new AuthenticationResult(OpenAuthenticationStatus.UserDoesNotExist,
                            new KeyValuePair<string, string>("AccessDenied", "User does not exist on system"));
                }
            }
            if (userFound == null) {
                _openAuthenticationService.AssociateExternalAccountWithUser(userLoggedIn, parameters);
            }

            _authenticationService.SignIn(userFound ?? userLoggedIn, false);

            return new AuthenticationResult(OpenAuthenticationStatus.Authenticated);
        }

        private bool RegistrationIsEnabled(RegistrationSettingsPart registrationSettings) {
            return registrationSettings.UsersCanRegister && !_openAuthenticationService.GetSettings().Record.AutoRegisterEnabled;
        }

        private bool AutoRegistrationIsEnabled(RegistrationSettingsPart registrationSettings) {
            return registrationSettings.UsersCanRegister && _openAuthenticationService.GetSettings().Record.AutoRegisterEnabled;
        }

        private bool AccountDoesNotExistAndUserIsNotLoggedOn(IUser userFound, IUser userLoggedIn) {
            return userFound == null && userLoggedIn == null;
        }

        private bool AccountIsAssignedToLoggedOnAccount(IUser userFound, IUser userLoggedIn) {
            return userFound.Id.Equals(userLoggedIn.Id);
        }

        private bool AccountAlreadyExistsAndUserIsLoggedOn(IUser userFound, IUser userLoggedIn) {
            return userFound != null && userLoggedIn != null;
        }

        private bool CanCreateAccount(OpenAuthenticationParameters parameters) {
            return new RegistrationDetails(parameters).IsValid();
        }

        private IUser CreateUser(OpenAuthenticationParameters parameters) {
            var details = new RegistrationDetails(parameters);
            var randomPassword = new Byte[10].ToString();
            return _membershipService.CreateUser(new CreateUserParams(details.UserName, randomPassword, details.EmailAddress, null, null, true));
        }
    }
}
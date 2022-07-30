# VampireCommandFramework
Framework for VRising mods to Easily build chat commands.

---


# ALPHA VERSION WARNING
## Please do not ship plugins using this version yet 

Please any questions @deca on VRising discord.

### Relative to RFC
Sharing source to build more in the open so folks better know what to anticipate and can give real feedback based on prototype usage. I was not keeping the RFC up to date with every code change and there wasn't any real activity on Github anyways. The implementation is largely a superset of the RFC. The major change is moving to a model where overloaded commands must have unique number of parameters simplifies things and allows for a Parse vs TryParse model and all sorts of common argument parsing error handling/response outside of the command body.
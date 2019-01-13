const TOKEN = "3BF2TCJt2j4t8mWC4kRxuJl1";
const HOST = "http://10.33.67.13";
vault = require("node-vault")(options);
var options = {
    apiVersion: "v1",
    endPoint: HOST,
    token: TOKEN
};

metadataCached = {};
isFirst = true;

exports.processSecrets = async function processSecrets() {
    var newSecrets = [];
    var metadataResult = await vault.read('secret/metadata?list=true');
    var keys = metadataResult.data.keys;
    var length = keys.length;
    for (var i = 0; i < length; i++) {
        try {
            var key = keys[i];
            var keyMetadata = await vault.read(`secret/metadata/${key}`);
            var versions = keyMetadata.data.versions;
            var isNewMetadata = false;
            if (metadataCached[key] == undefined) {
                metadataCached[key] = {};
                isNewMetadata = true;
            }
            for (var versionNum in versions) {
                var version = versions[versionNum];
                var isDeleted = !(version.deletion_time == "");
                if (metadataCached[key][versionNum] == undefined) {
                    metadataCached[key][versionNum] = isDeleted;
                    isNewMetadata = true;
                }
                else if (metadataCached[key][versionNum] != isDeleted) {
                    metadataCached[key][versionNum] = isDeleted;
                    isNewMetadata = true;
                }
                if(isNewMetadata && !isFirst)
                {
                    var newSecret = {
                        key : key,
                        version : versionNum,
                        isDeleted : isDeleted
                    };
                    //newSecrets.push(newSecret);
                    console.log(`new secret with properties ${newSecret.key}  ${newSecret.isDeleted} ${newSecret.version}`);
                }
            }
            if (i == length - 1) {
                if(isFirst)
                {
                    isFirst = false;
                }
                console.log('loop finished ');
            }
        }
        catch (exc) {
            console.log(exc);
        }
    }
}




////
/*
.then(result =>
                    {
                        var versions = result.data.versions;
                        var isNewMetadata = false;
                        if(this.metadataCached[key] == undefined)
                        {
                            this.metadataCached[key] = {};
                            isNewMetadata = true;
                        }
                        for(var versionNum in versions)
                        {
                            var version = versions[versionNum];
                            var isDeleted = !(version.deletion_time == "");
                            if(this.metadataCached[key][version] == undefined)
                            {
                                this.metadataCached[key][version] = isDeleted;
                                isNewMetadata = true;
                            }
                            else if(this.metadataCached[key][version] != isDeleted)
                            {
                                this.metadataCached[key][version] = isDeleted;
                                isNewMetadata = true;
                            }
                        }
                    }).catch(exc => {
                        console.log(exc);
                    });
                 

                   .then(result => {
                    

*/

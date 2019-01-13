'use strict';
const projectId = "api-7805202409062569548-609752";
const topicName = "vaultchange";
const {PubSub} = require('@google-cloud/pubsub');
const pubsub = new PubSub({
    projectId : projectId
});
var http = require('http');
var request = require('request');
var gitlabUrl = 'http://git.questrade.com/api/v4/projects/850/trigger/pipeline';
var token = '0bb67dad53fa26c0833567eded0a0c'
var masterBranch = 'master';
var isFirst = true;
var maxDateDiff = 10000; //10s between requests
var previousDate = Date.now();
var currentDate = Date.now();
var consul = require('consul')({
    "host": "35.192.9.171",
    "port" : 80
});
var port = 1339;
let sent = false;

let publishChange = async () => {
    const data = "changenotif";
    const dataBuffer = Buffer.from(data);
    const messageId = await pubsub
    .topic(topicName)
    .publisher()
    .publish(dataBuffer);
    sent = false;
    console.log(`Message ${messageId} published.`);

}
let watchPath = "vault";
var watch = consul.watch({ method: consul.kv.get, options: { key: watchPath } });
var options = {
    url: gitlabUrl,
    method: 'POST',
    form: {
        token: token,
        ref: masterBranch
    }
};

watch.on('change', function (data, res) {
    if(isFirst)
    {
       isFirst = false;
       return;
    }
    if(sent == false)
    {
        setTimeout(publishChange,6000);
        sent = true;
    }

});

watch.on('error', function (err) {
    console.log('error:', err);
});

//curl - X POST \
//-F token = 0bb67dad53fa26c0833567eded0a0c \
//http://git.questrade.com/api/v4/projects/850/trigger/pipeline

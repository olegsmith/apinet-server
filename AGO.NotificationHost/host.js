var app = require('http').createServer(handler)
  , io = require('socket.io').listen(app, {resource: '/socket'})
  , redis = require("redis")
  , pg = require('pg')
  , client = redis.createClient(6379, '127.0.0.1', {retry_max_delay: 1000 * 60})
  , login2socket = {}
  , connStr = 'pg://ago_user:123@localhost:5432/ago_apinet'
  , listenPort = process.argv && process.argv.length > 2 ? process.argv[2] : 36653;

 pg.on('error', function(err, client) {
	console.error('Error on postgresql connection', err);
 });
  
io.configure(function() {
	io.set('log level', 2);
	io.set('authorization', function (handshakeData, callback) {
		if (handshakeData.login) {
			callback(null, true);
			return;
		}

		if (!handshakeData.query || !handshakeData.query.token) {
			callback("No auth token", false);
			return;
		}

		var token = handshakeData.query.token;
		pg.connect(connStr, function(err, client, done) {
			if(err) {
				callback("Can not validate token due to internal error", false);
				return console.error('Error fetching client from pool', err);
			}
			
			client.on('error', function(error) { console.error('Error resolve token ' + token + ' to login', error); }); 
			client.query('select "Login" from "Core"."TokenToLogin" where "Token" = $1', [token], function(err, result) {
				done();
				
				if(err) {
					callback("Can not validate token due to internal error", false);
					return console.error('Error resolve token ' + token + ' to login', err);
				}
				
				if (result && result.rows && result.rows.length > 0) {
					handshakeData.login = result.rows[0].Login;
					callback(null, true);	
					return;
				}
				
				callback("Invalid token", false);
			});
		});
	});
})

app.listen(listenPort);

client.on('error', function(err) { console.error(err); } );
client.on('ready', function () {
	client.subscribe('reports_changed');
	client.subscribe('workqueue_changed');
});

io.sockets.on('connection', function (s) {
	var login = s.handshake.login;
	login2socket[login] = login2socket[login] || { login: login, sockets: [] };
	login2socket[login].sockets.push(s);

	s.on('disconnect', function() {
		if (!login2socket[login]) {
			//already no data for this login
			return;
		}
		var socketIndex = login2socket[login].sockets.indexOf(s);
		if (socketIndex >= 0) {
			login2socket[login].sockets.splice(socketIndex, 1);
		}
		if (login2socket[login].sockets.length <= 0) {
			delete login2socket[login];
		}
	});
});

client.on('message', function(channel, message) {
	//assume, that message is Object#{login, ...any other fields}
	var parsedMsg = JSON.parse(message);
	if (!login2socket[parsedMsg.login]) {
		console.log('User ' + parsedMsg.login + ' not connected, remove message');
		console.log(login2socket);
		return;
	}
	
	var socks = login2socket[parsedMsg.login].sockets;
	delete parsedMsg['login'];//sensitive information, not needed on client, remove
	for(var i = 0; i < socks.length; i++) {
		socks[i].emit(channel, parsedMsg);
	}
});

function handler (req, res) {
  res.writeHead(200);
  res.end("Ok, i am notification host");
}

"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
const http_1 = __importDefault(require("http"));
const express_1 = __importDefault(require("express"));
const cors_1 = __importDefault(require("cors"));
const core_1 = require("@colyseus/core");
const mongoose_1 = __importDefault(require("mongoose"));
const GameRoom_1 = require("./rooms/GameRoom");
const ws_transport_1 = require("@colyseus/ws-transport");
const port = Number(process.env.PORT || 2567);
const mongoUri = process.env.MONGO_URI || "mongodb://localhost:27017/pokemon_mmo";
const app = (0, express_1.default)();
app.use((0, cors_1.default)());
app.use(express_1.default.json());
// Create HTTP and Colyseus Servers
const server = http_1.default.createServer(app);
const gameServer = new core_1.Server({
    transport: new ws_transport_1.WebSocketTransport({ server }),
});
// Define your MMO World room
gameServer.define('mmo_world', GameRoom_1.GameRoom);
/**
 * Start Server after MongoDB connection
 */
async function start() {
    try {
        console.log(`[Database] Connecting to: ${mongoUri}...`);
        await mongoose_1.default.connect(mongoUri);
        console.log('✅ MongoDB Connected.');
        server.listen(port, () => {
            console.log(`✅ Pokémon MMO Server listening on http://localhost:${port}`);
        });
    }
    catch (error) {
        console.error('❌ Server failed to start:', error);
        process.exit(1);
    }
}
start();

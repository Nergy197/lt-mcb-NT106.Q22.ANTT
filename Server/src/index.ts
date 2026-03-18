import http from 'http';
import express from 'express';
import cors from 'cors';
import { Server } from '@colyseus/core';
import mongoose from 'mongoose';
import { GameRoom } from './rooms/GameRoom';
import { WebSocketTransport } from '@colyseus/ws-transport';

const port = Number(process.env.PORT || 2567);
const mongoUri = process.env.MONGO_URI || "mongodb://localhost:27017/pokemon_mmo";

const app = express();
app.use(cors());
app.use(express.json());

// Create HTTP and Colyseus Servers
const server = http.createServer(app);
const gameServer = new Server({
  transport: new WebSocketTransport({ server }),
});

// Define your MMO World room
gameServer.define('mmo_world', GameRoom);

/**
 * Start Server after MongoDB connection
 */
async function start() {
  try {
    console.log(`[Database] Connecting to: ${mongoUri}...`);
    await mongoose.connect(mongoUri);
    console.log('✅ MongoDB Connected.');

    server.listen(port, () => {
      console.log(`✅ Pokémon MMO Server listening on http://localhost:${port}`);
    });

  } catch (error) {
    console.error('❌ Server failed to start:', error);
    process.exit(1);
  }
}

start();

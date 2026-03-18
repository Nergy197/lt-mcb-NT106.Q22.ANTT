import mongoose from 'mongoose';

// Models
export * from './models/Account';
export * from './models/Player';
export * from './models/PokemonInstance';
export * from './models/PokemonStats';
export * from './models/PokemonMoves';

// Services
export * from './services/GameService';

/**
 * Initialize MongoDB Connection for the Game Server
 * @param uri MongoDB Connection String
 */
export const connectDatabase = async (uri: string): Promise<void> => {
  try {
    await mongoose.connect(uri);
    console.log('✅ Connected to MongoDB successfully.');
  } catch (error) {
    console.error('❌ MongoDB Connection Error:', error);
    process.exit(1);
  }
};

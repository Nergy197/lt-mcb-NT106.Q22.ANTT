import mongoose, { Document, Types } from 'mongoose';
export interface IPokemonMoves extends Document {
    pokemon_instance_id: Types.ObjectId;
    move_id: number;
    slot: number;
    current_pp: number;
}
export declare const PokemonMovesModel: mongoose.Model<IPokemonMoves, {}, {}, {}, mongoose.Document<unknown, {}, IPokemonMoves, {}, mongoose.DefaultSchemaOptions> & IPokemonMoves & Required<{
    _id: Types.ObjectId;
}> & {
    __v: number;
} & {
    id: string;
}, any, IPokemonMoves>;
//# sourceMappingURL=PokemonMoves.d.ts.map
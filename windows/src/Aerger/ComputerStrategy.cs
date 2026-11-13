namespace Aerger;

// Direct translation of seg06:0022 (score) and 0640 (choose).
// Smaller scores win; ties retain the first legal piece, in piece-number order.
public static class ComputerStrategy
{
    public static int Choose(Game game)
    {
        var moves=game.LegalMoves();
        if(moves.Length==0)throw new InvalidOperationException("Kein Zug möglich.");
        return moves.OrderBy(i=>Score(game,i)).First();
    }
    public static int Score(Game g,int piece)
    {
        int from=g.Position(g.Player,piece), to=g.Target(piece);
        if(from<0)return 0;                                      // 0037
        if(from>=g.TrackLength)return -3;                        // 0045
        if(to>=g.TrackLength)return -2;                          // 005a
        int square=g.Square(g.Player,from), target=g.Square(g.Player,to), score=0;
        int stride=g.TrackLength/g.PlayerCount;
        int Occupant(int field)
        {
            for(int p=0;p<g.PlayerCount;p++)
                if(g.Kinds[p]!=PlayerKind.Inactive)
                    for(int i=0;i<4;i++)
                    {int at=g.Position(p,i);if(at>=0&&at<g.TrackLength&&g.Square(p,at)==field)return p;}
            return -1;
        }
        int Finish(int p)=>p==0?g.TrackLength-1:p*stride-1;
        bool NearFinish(int field,int p)=>field>=Finish(p)-7&&field<=Finish(p);
        int opponent=Occupant(target);
        if(opponent>=0)score-=NearFinish(target,opponent)?2:1;     // 00e6..025d
        if(g.Rules.Rest&&g.IsRestSquare(target))score--;          // 0260
        if(NearFinish(square,g.Player))score--;                  // 02bb
        if(g.Rules.Rest&&g.IsRestSquare(square))score++;          // 0363
        for(int p=0;p<g.PlayerCount;p++)                         // 039d
        {
            if(p==g.Player||g.Kinds[p]==PlayerKind.Inactive)continue;
            if(!Enumerable.Range(0,4).Any(i=>g.Position(p,i)<0))continue;
            if(target==p*stride)score++;
            if(square==p*stride)score--;
        }
        for(int step=1;step<g.Die;step++)                         // 055b
        {int p=Occupant((square+step)%g.TrackLength);if(p>=0&&p!=g.Player)score++;}
        return score;
    }
}

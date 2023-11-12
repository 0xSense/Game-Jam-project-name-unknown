 /*
 @author Alexander Venezia (Blunderguy)
*/

namespace Combat;

using Data;
using Godot;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Systems.Combat;

public enum PlayerState
{
    SELECTING_CARD,
    SELECTING_TARGETS,
    GAME_OVER
}

public partial class Player : Combatant
{ 
    [Export] Hand _hand;
    [Export] RichTextLabel _targetLabel;
    [Export] Label _actionPointLabel;
    [Export] Label _movementPointLabel;

    private PlayerState _state;
    public PlayerState State => _state;

    private Card _currentlyTargeting;
    private List<ICombatant> _targeted = new();

    public override void _Ready()
    {
        base._Ready();
        _state = PlayerState.SELECTING_CARD;
    }

    private void SyncDeck()
    {        
        _internalDeck = MasterDeck.PlayerDeck;
    }

    public override void StartFight()
    {
        base.StartFight();
        SyncDeck();
        _internalDeck.ForceShuffle();
        _hand.DrawOpeningHand(_internalDeck);
    }

    public override void BeginTurn()
    {
        base.BeginTurn();
        _actionPointLabel.Text = _actionPoints.ToString();
        _movementPointLabel.Text = _movementPoints.ToString();
        GD.Print("Begin player turn");
    }

    public override void _PhysicsProcess(double delta)
    {
        void endTargeting()
        {
            _currentlyTargeting = null;
            _hand.Unfreeze();
            _targetLabel.Visible = false;
            _state = PlayerState.SELECTING_CARD;
            _targeted = new();
        }
        if (!_isTurn)
            return;        

        if (Input.IsActionJustPressed("Select"))
        {
            switch (_state)
            {
                case PlayerState.SELECTING_CARD:
                    
                    _currentlyTargeting = _hand.GetSelectedCard();

                    if (_currentlyTargeting != null && CanPlay(_currentlyTargeting))
                    {
                        _state = PlayerState.SELECTING_TARGETS;
                        _targetLabel.Visible = true;
                        _hand.Freeze();
                        _currentlyTargeting.ZIndex += 99;
                    }

                break;     
                case PlayerState.SELECTING_TARGETS:
                    Enemy clicked = GetEnemyUnderMouse();
                    if (clicked != null)
                    {
                        _targeted.Add(clicked);
                    }

                    if (IsTargetingValid())
                    {
                        Vector2 position = _currentlyTargeting.GlobalPosition;
                        _hand.RemoveCard(_currentlyTargeting);
                        _currentlyTargeting.Position = position;
                        GetParent().AddChild(_currentlyTargeting);

                        PlayCard(_targeted.ToArray(), _currentlyTargeting);                                            
                        
                        endTargeting();
                    }
                break;      
                case PlayerState.GAME_OVER:
                break;     
            }
        }

        if (Input.IsActionJustPressed("Deselect") && _state == PlayerState.SELECTING_TARGETS)
        {
            endTargeting();
        }
    }

    private async void PlayCard(ICombatant[] targets, Card card)
    {
        await card.BeginPlayAnimation();
        _combatManager.PlayCard(this, targets, card.Data);
        _internalDeck.Discard(card.Data);

        _actionPointLabel.Text = _actionPoints.ToString();
        _movementPointLabel.Text = _movementPoints.ToString();
    }

    /* Enemies are not supposed to overlap, so no need for z-checking.*/
    private Enemy GetEnemyUnderMouse()
    {
        Enemy enemy = null;

        Vector2 mousePos = GetGlobalMousePosition();
        PhysicsDirectSpaceState2D spaceState = GetWorld2D().DirectSpaceState;
        PhysicsPointQueryParameters2D query = new();
        query.CollideWithAreas = true;
        query.Position = mousePos;
        query.CollisionMask = Enemy.Bitmask;
        var hits = spaceState.IntersectPoint(query);

        if (hits.Count == 0)
            return null;
            
        enemy = (Enemy)hits.ElementAt(0)["collider"];

        return enemy;
    }

    private bool CanPlay(Card card)
    {
        return (card.Data.ActionPointCost <= _actionPoints) && (card.Data.MovementPointCost <= _movementPoints);
    }

    private bool IsTargetingValid()
    {
        switch (_currentlyTargeting.Data.Target)
        {
            case TargetType.SELF:
            return true;
            case TargetType.SINGLE:
            return _targeted.Count == 1;
            case TargetType.MULTI_TWO:
            return _targeted.Count == 2 || (_targeted.Count >= _combatManager.GetRemainingEnemies());
            case TargetType.MULTI_THREE:
            return _targeted.Count == 3 || (_targeted.Count >= _combatManager.GetRemainingEnemies());
            case TargetType.MULTI_FOUR:
            return true;
            case TargetType.ALL:
            return true;
            default:
            return false;
        }
    }


    public async void OnEndTurnButtonInput(Viewport node, InputEvent e, int shapeID)
    {
        if (e.IsActionPressed("Select"))
        {
            if (_isTurn)
            {
                _isTurn = false;
                await Task.Delay(1000);
                this.EndTurn();
            }
        }
    }

    public override void DrawCards(int n)
    {
        CardData[] cards = _internalDeck.Draw(n);
        _hand.AddCards(cards);
    }

    public override void DiscardCards(int n)
    {

    }

    public override void ReturnCards(int n)
    {

    }

    public override void EndFight(EndState result)
    {
        base.EndFight(result);
        GD.Print("Fight Over - You emerge in " + result);
        _state = PlayerState.GAME_OVER;
        ((CombatMain)GetParent()).EndFight(result);
    }

}

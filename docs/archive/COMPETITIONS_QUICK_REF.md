# ?? Competitions - Quick Reference Card

## ?? Competition Formats at a Glance

| Format | Players | Rounds | Matches | Best Use Case |
|--------|---------|--------|---------|---------------|
| **Singles KO** | 2-128 | log?(n) | n-1 | Quick tournaments, playoffs |
| **Doubles KO** | 2-64 pairs | log?(n) | n-1 | Social events, variety |
| **Team KO** | 2-32 teams | log?(n) | n-1 | Cup competitions, inter-division |
| **Round Robin** | 2-16 | n-1 | n(n-1)/2 | Mini-leagues, qualifiers |
| **Swiss** | 4-256 | Custom | Custom | Large tournaments |

---

## ? Quick Actions

| What You Want | Steps |
|---------------|-------|
| **Create Tournament** | Competitions Tab ? New ? Enter Name ? Select Format |
| **Add Players** | Select Competition ? Add Participant ? Choose Players |
| **Generate Bracket** | Add ?2 Participants ? Generate Bracket |
| **Enter Scores** | View Bracket ? Enter Scores in Fields |
| **View Results** | View Bracket ? See Winners/Progression |

---

## ?? Match Count Calculator

```
Singles Knockout:
  4 players  = 3 matches
  8 players  = 7 matches
  16 players = 15 matches
  32 players = 31 matches

Round Robin:
  4 players = 6 matches  (3 rounds)
  6 players = 15 matches (5 rounds)
  8 players = 28 matches (7 rounds)
```

---

## ?? UI Layout

```
???????????????????????????????????????
?     ?? COMPETITIONS TAB             ?
???????????????????????????????????????
?              ?                      ?
? Competition  ?  ?? Details Panel    ?
? List         ?                      ?
?              ?  Name: [________]    ?
? ???????????? ?  Format: [v]        ?
? ?  Winter  ? ?  Status: [v]        ?
? ?  Champ.  ? ?                      ?
? ? 16 entry ? ?  ?? Participants     ?
? ???????????? ?  [Add] [Clear]       ?
?              ?                      ?
? [ + New  ]   ?  ?? Bracket          ?
? [ Delete ]   ?  [Generate] [View]   ?
?              ?                      ?
? Status: ?    ?  [ Save Changes ]    ?
???????????????????????????????????????
```

---

## ?? Pro Tips

### **For Singles Tournaments**
? Aim for power-of-2 participants (4, 8, 16, 32) for clean brackets
? Odd numbers get byes in first round automatically
? Random seeding currently - manual ordering coming soon

### **For Team Competitions**
? Perfect for end-of-season playoffs
? Winner of each division ? knockout cup
? Great for inter-division rivalry events

### **For Round Robin**
? Best with 4-8 participants (manageable match count)
? Everyone gets multiple matches
? Fair format when skill levels vary

### **General Best Practices**
? Set competition to "Draft" while planning
? Add all participants before generating bracket
? Generate bracket triggers "InProgress" status
? Mark "Completed" when tournament finishes

---

## ?? Common Issues & Fixes

| Issue | Solution |
|-------|----------|
| Can't generate bracket | Need at least 2 participants |
| Bracket not showing | Click "Generate Bracket" first |
| Player not in list | Check current season |
| Can't save scores | Click "Save Changes" after entry |
| Want to restart bracket | Delete and create new competition |

---

## ?? Example Workflows

### **Quick 8-Player Friday Night**
1. New ? "Friday Singles"
2. Format: Singles Knockout
3. Add 8 players ? Generate
4. Result: 7 matches, 3 rounds
5. Time: ~2-3 hours

### **Mini-League (6 Players)**
1. New ? "Mini Round Robin"
2. Format: Round Robin
3. Add 6 players ? Generate
4. Result: 15 matches, 5 rounds
5. Time: ~3-4 weeks

### **Division Cup (16 Teams)**
1. New ? "Champions Cup"
2. Format: Team Knockout
3. Add 16 teams ? Generate
4. Result: 15 matches, 4 rounds
5. Time: ~4-8 weeks

---

## ?? Keyboard Shortcuts (Planned)

| Key | Action |
|-----|--------|
| `Ctrl+N` | New Competition |
| `Ctrl+S` | Save Changes |
| `Delete` | Delete Competition |
| `Ctrl+G` | Generate Bracket |
| `Ctrl+V` | View Bracket |

---

## ?? Status Indicators

| Status | Icon | Meaning |
|--------|------|---------|
| **Draft** | ?? | Planning stage |
| **InProgress** | ?? | Matches ongoing |
| **Completed** | ? | Tournament finished |

---

## ?? Bracket Round Names

```
128 players ? Round of 128
 64 players ? Round of 64
 32 players ? Round of 32
 16 players ? Round of 16
  8 players ? Quarter-Finals
  4 players ? Semi-Finals
  2 players ? Final
```

---

## ?? Mobile View (Future)

Current layout adapts to:
- Desktop: Side-by-side panels
- Tablet: Stacked panels
- Mobile: Single-column scroll

---

## ?? When to Use Each Format

### **Singles Knockout** ?
- ? Quick results needed
- ? Clear winner required
- ? Traditional tournament feel
- ? Some players eliminated early

### **Doubles Knockout** ?
- ? Social/fun events
- ? Team building
- ? Variety from singles play
- ? Requires pair coordination

### **Team Knockout** ?
- ? Inter-team competition
- ? Playoff structure
- ? Division champions event
- ? Requires multiple team members available

### **Round Robin** ?
- ? Everyone gets multiple matches
- ? Fair for all skill levels
- ? Good for rankings/seeding
- ? Many matches required
- ? Can take longer

### **Swiss (Coming Soon)** ?
- ? Large player pools
- ? No elimination
- ? Competitive balance
- ? More complex to manage

---

## ?? Color Coding (In UI)

- **Blue:** Active/selected competition
- **Green:** Success messages, completed
- **Red:** Delete buttons, errors
- **Gray:** Draft status, inactive

---

## ?? Future Feature Preview

Coming Soon:
- ?? Visual bracket tree diagram
- ?? PDF export
- ?? Match scheduling
- ?? Statistics & analytics
- ?? Mobile-optimized view
- ?? Leaderboards
- ?? Custom seeding

---

## ?? Quick Help

**Need Help?**
- Check `COMPETITIONS_GUIDE.md` for full documentation
- Review examples in guide
- Check troubleshooting section

**Feature Request?**
- Note desired features
- Check "Future Enhancements" section
- Consider contributing ideas

---

## ?? Remember

? Always save changes
? Season must be selected
? Minimum 2 participants required
? Generate bracket before viewing
? Backup data before major tournaments

---

## ?? Happy Tournament Running!

You're all set to organize amazing competitions! ????
